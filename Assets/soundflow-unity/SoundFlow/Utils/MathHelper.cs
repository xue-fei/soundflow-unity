using System;
using System.Numerics;
using Unity.Burst.Intrinsics;
using static Unity.Burst.Intrinsics.X86;

namespace SoundFlow.Utils
{

    /// <summary>
    ///     Helper methods for common math operations.
    /// </summary>
    public static class MathHelper
    {
        /// <summary>
        /// Computes the Inverse Fast Fourier Transform (IFFT) of a complex array.
        /// </summary>
        /// <param name="data">The complex data array.</param>
        public static void InverseFft(Complex[] data)
        {
            // Conjugate the complex data
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = Complex.Conjugate(data[i]);
            }

            // Perform FFT
            Fft(data);

            // Conjugate and scale the result
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = Complex.Conjugate(data[i]);
            }
        }

        /// <summary>
        /// Computes the Fast Fourier Transform (FFT) of a complex array using SIMD acceleration with fallback to a scalar implementation.
        /// </summary>
        /// <param name="data">The complex data array. Must be a power of 2 in length.</param>
        public static void Fft(Complex[] data)
        {
            var n = data.Length;
            if (n <= 1) return;

            if (Avx.IsAvxSupported && n >= 8) // Use AVX for larger arrays
                FftAvx(data);
            else if (Sse2.IsSse2Supported && n >= 4) // Use SSE2 for smaller arrays
                FftSse2(data);
            else // Fallback to scalar implementation
                FftScalar(data);
        }

        /// <summary>
        /// Scalar implementation of the Fast Fourier Transform (FFT).
        /// </summary>
        /// <param name="data">The complex data array. Must be a power of 2 in length.</param>
        private static void FftScalar(Complex[] data)
        {
            var n = data.Length;
            if (n <= 1) return;

            // Separate even and odd elements
            var even = new Complex[n / 2];
            var odd = new Complex[n / 2];
            for (var i = 0; i < n / 2; i++)
            {
                even[i] = data[2 * i];
                odd[i] = data[2 * i + 1];
            }

            // Recursive FFT on even and odd parts
            FftScalar(even);
            FftScalar(odd);

            // Combine
            for (var k = 0; k < n / 2; k++)
            {
                var t = Complex.FromPolarCoordinates(1.0, -2.0 * Math.PI * k / n) * odd[k];
                data[k] = even[k] + t;
                data[k + n / 2] = even[k] - t;
            }
        }

        /// <summary>
        /// SSE2-accelerated implementation of the Fast Fourier Transform (FFT).
        /// </summary>
        /// <param name="data">The complex data array. Must be a power of 2 in length and at least 4.</param>
        private static unsafe void FftSse2(Complex[] data)
        {
            var n = data.Length;

            // Bit-reverse the data
            BitReverse(data);

            // Cooley-Tukey FFT algorithm with SSE2
            for (var s = 1; s <= Math.Log(n, 2); s++)
            {
                var m = 1 << s;
                var m2 = m >> 1;

                var wm = new v128(Complex.FromPolarCoordinates(1.0, -Math.PI / m2).Real,
                    Complex.FromPolarCoordinates(1.0, -Math.PI / m2).Imaginary);

                for (var k = 0; k < n; k += m)
                {
                    var w = new v128(1.0, 0.0);
                    for (var j = 0; j < m2; j += 2)
                    {
                        fixed (Complex* pData = &data[0])
                        {
                            // Load even and odd elements

                            var even1 = Sse2.loadu_si128((double*)(pData + k + j));
                            var odd1 = Sse2.loadu_si128((double*)(pData + k + j + m2));

                            var even2 = Sse2.loadu_si128((double*)(pData + k + j + 2));
                            var odd2 = Sse2.loadu_si128((double*)(pData + k + j + m2 + 2));

                            // Calculate twiddle factors
                            var twiddle1 = MultiplyComplexSse2(odd1, w);

                            // Update w
                            w = MultiplyComplexSse2(w, wm);
                            var twiddle2 = MultiplyComplexSse2(odd2, w);
                            w = MultiplyComplexSse2(w, wm);

                            // Butterfly operations

                            Sse2.store_si128((double*)(pData + k + j), Sse2.add_pd(even1, twiddle1));
                            Sse2.store_si128((double*)(pData + k + j + m2), Sse2.sub_pd(even1, twiddle1));

                            Sse2.store_si128((double*)(pData + k + j + 2), Sse2.add_pd(even2, twiddle2));
                            Sse2.store_si128((double*)(pData + k + j + m2 + 2), Sse2.sub_pd(even2, twiddle2));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// AVX-accelerated implementation of the Fast Fourier Transform (FFT).
        /// </summary>
        /// <param name="data">The complex data array. Must be a power of 2 in length and at least 8.</param>
        private static unsafe void FftAvx(Complex[] data)
        {
            int n = data.Length;
            BitReverse(data);
            
            for (int s = 1; s <= Math.Log(n, 2); s++)
            {
                int m = 1 << s;
                int m2 = m >> 1;
                
                if (m < 8)
                {
                    // 小规模使用标量
                    for (int k = 0; k < n; k += m)
                    {
                        for (int j = 0; j < m2; j++)
                        {
                            Complex t = Complex.FromPolarCoordinates(1.0, -2.0 * Math.PI * j / m) * data[k + j + m2];
                            Complex tmp = data[k + j];
                            data[k + j] = tmp + t;
                            data[k + j + m2] = tmp - t;
                        }
                    }
                    continue;
                }

                var wm = new v256(
                    Complex.FromPolarCoordinates(1.0, -Math.PI / m2).Real,
                    Complex.FromPolarCoordinates(1.0, -Math.PI / m2).Imaginary,
                    Complex.FromPolarCoordinates(1.0, -Math.PI / m2).Real,
                    Complex.FromPolarCoordinates(1.0, -Math.PI / m2).Imaginary
                );

                for (int k = 0; k < n; k += m)
                {
                    var w = new v256(1.0, 0.0, 1.0, 0.0);
                    for (int j = 0; j < m2; j += 2)
                    {
                        if (j + 1 >= m2) break;

                        fixed (Complex* pData = &data[0])
                        {
                            double* ptr = (double*)(pData + k + j);
                            double* ptrOdd = (double*)(pData + k + j + m2);
                            
                            v256 even = X86.Avx.mm256_loadu_pd(ptr);
                            v256 odd = X86.Avx.mm256_loadu_pd(ptrOdd);
                            
                            v256 twiddle = MultiplyComplexAvx(odd, w);
                            w = MultiplyComplexAvx(w, wm);
                            
                            v256 resultEven = X86.Avx.mm256_add_pd(even, twiddle);
                            v256 resultOdd = X86.Avx.mm256_sub_pd(even, twiddle);
                            
                            X86.Avx.mm256_storeu_pd(ptr, resultEven);
                            X86.Avx.mm256_storeu_pd(ptrOdd, resultOdd);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Bit-reverses the order of elements in a complex array.
        /// </summary>
        /// <param name="data">The complex data array. Must be a power of 2 in length.</param>
        private static void BitReverse(Complex[] data)
        {
            var n = data.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                var bit = n >> 1;
                for (; (j & bit) > 0; bit >>= 1)
                {
                    j ^= bit;
                }

                j ^= bit;

                if (i < j)
                {
                    (data[j], data[i]) = (data[i], data[j]);
                }
            }
        }

        /// <summary>
        /// Multiplies two complex numbers represented as Vector128.
        /// </summary>
        /// <param name="a">The first complex number (real, imaginary).</param>
        /// <param name="b">The second complex number (real, imaginary).</param>
        /// <returns>The result of complex multiplication (real, imaginary).</returns>
        private static v128 MultiplyComplexSse2(v128 a, v128 b)
        {
            // 提取实部和虚部
            v128 aImRe = X86.Sse2.shuffle_pd(a, a, 0b01); // [a_im, a_re]
            v128 bImRe = X86.Sse2.shuffle_pd(b, b, 0b01); // [b_im, b_re]

            // 计算实部: (a_re * b_re) - (a_im * b_im)
            // 计算虚部: (a_re * b_im) + (a_im * b_re)
            v128 realPart = X86.Sse2.mul_pd(a, b);
            v128 imagPart = X86.Sse2.mul_pd(aImRe, bImRe);

            // 组合结果: [real, imag]
            return X86.Sse2.add_pd(realPart, imagPart);
        }

        /// <summary>
        /// Multiplies two complex numbers represented as Vector256.
        /// </summary>
        /// <param name="a">The first complex number (real, imaginary, real, imaginary).</param>
        /// <param name="b">The second complex number (real, imaginary, real, imaginary).</param>
        /// <returns>The result of complex multiplication (real, imaginary, real, imaginary).</returns>
        private static v256 MultiplyComplexAvx(v256 a, v256 b)
        {
            // 交换实部虚部
            v256 aImRe = X86.Avx.mm256_permute_pd(a, 0b0101);
            v256 bImRe = X86.Avx.mm256_permute_pd(b, 0b0101);

            // 计算实部和虚部
            v256 realPart = X86.Avx.mm256_mul_pd(a, b);
            v256 imagPart = X86.Avx.mm256_mul_pd(aImRe, bImRe);

            // 实部相减，虚部相加
            v256 sign = new v256(1.0, -1.0, 1.0, -1.0);
            imagPart = X86.Avx.mm256_mul_pd(imagPart, sign);

            return X86.Avx.mm256_add_pd(realPart, imagPart);
        }

        /// <summary>
        /// Generates a Hamming window of a specified size using SIMD acceleration with fallback to a scalar implementation.
        /// </summary>
        /// <param name="size">The size of the Hamming window.</param>
        /// <returns>The Hamming window array.</returns>
        public static float[] HammingWindow(int size)
        {
            if (X86.Avx.IsAvxSupported && size >= 8)
                return HammingWindowAvx(size);

            if (X86.Sse.IsSseSupported && size >= 4)
                return HammingWindowSse(size);

            return HammingWindowScalar(size);
        }

        /// <summary>
        /// Generates a Hamming window using a scalar implementation.
        /// </summary>
        /// <param name="size">The size of the Hamming window.</param>
        /// <returns>The Hamming window array.</returns>
        private static float[] HammingWindowScalar(int size)
        {
            var window = new float[size];
            for (var i = 0; i < size; i++)
            {
                window[i] = 0.54f - 0.46f * MathF.Cos((2 * MathF.PI * i) / (size - 1));
            }

            return window;
        }

        /// <summary>
        /// Generates a Hamming window using SSE acceleration.
        /// </summary>
        /// <param name="size">The size of the Hamming window.</param>
        /// <returns>The Hamming window array.</returns>
        private static unsafe float[] HammingWindowSse(int size)
        {
            float[] window = new float[size];
            int vectorSize = 4;
            int remainder = size % vectorSize;

            fixed (float* pWindow = window)
            {
                v128 vConstA = new v128(0.54f);
                v128 vConstB = new v128(0.46f);
                v128 vTwoPi = new v128(2.0f * MathF.PI / (size - 1));

                for (int i = 0; i < size - remainder; i += vectorSize)
                {
                    v128 vIndices = new v128(i, i + 1, i + 2, i + 3);
                    v128 vCosArg = X86.Sse.mul_ps(vTwoPi, vIndices);
                    v128 vCos = FastCosineSse(vCosArg);
                    v128 vResult = X86.Sse.sub_ps(vConstA, X86.Sse.mul_ps(vConstB, vCos));

                    X86.Sse.storeu_ps(pWindow + i, vResult);
                }
            }

            // 处理剩余元素
            for (int i = size - remainder; i < size; i++)
            {
                window[i] = 0.54f - 0.46f * MathF.Cos(2 * MathF.PI * i / (size - 1));
            }

            return window;
        }

        /// <summary>
        /// Generates a Hamming window using AVX acceleration.
        /// </summary>
        /// <param name="size">The size of the Hamming window.</param>
        /// <returns>The Hamming window array.</returns>
        private static unsafe float[] HammingWindowAvx(int size)
        {
            float[] window = new float[size];
            int vectorSize = 8;
            int remainder = size % vectorSize;

            fixed (float* pWindow = window)
            {
                v256 vConstA = new v256(0.54f);
                v256 vConstB = new v256(0.46f);
                v256 vTwoPi = new v256(2.0f * MathF.PI / (size - 1));

                for (int i = 0; i < size - remainder; i += vectorSize)
                {
                    v256 vIndices = new v256(
                        i, i + 1, i + 2, i + 3,
                        i + 4, i + 5, i + 6, i + 7
                    );

                    v256 vCosArg = X86.Avx.mm256_mul_ps(vTwoPi, vIndices);
                    v256 vCos = FastCosineAvx(vCosArg);
                    v256 vResult = X86.Avx.mm256_sub_ps(vConstA, X86.Avx.mm256_mul_ps(vConstB, vCos));

                    X86.Avx.mm256_storeu_ps(pWindow + i, vResult);
                }
            }

            // 处理剩余元素
            for (int i = size - remainder; i < size; i++)
            {
                window[i] = 0.54f - 0.46f * MathF.Cos(2 * MathF.PI * i / (size - 1));
            }

            return window;
        }

        /// <summary>
        /// Generates a Hanning window of a specified size using SIMD acceleration with fallback to a scalar implementation.
        /// </summary>
        /// <param name="size">The size of the Hanning window.</param>
        /// <returns>The Hanning window array.</returns>
        public static float[] HanningWindow(int size)
        {
            if (X86.Avx.IsAvxSupported && size >= 8)
                return HanningWindowAvx(size);

            if (X86.Sse.IsSseSupported && size >= 4)
                return HanningWindowSse(size);

            return HanningWindowScalar(size);
        }


        /// <summary>
        /// Generates a Hanning window using a scalar implementation.
        /// </summary>
        /// <param name="size">The size of the Hanning window.</param>
        /// <returns>The Hanning window array.</returns>
        private static float[] HanningWindowScalar(int size)
        {
            var window = new float[size];
            for (var i = 0; i < size; i++)
            {
                window[i] = 0.5f * (1.0f - MathF.Cos((2 * MathF.PI * i) / (size - 1)));
            }

            return window;
        }

        /// <summary>
        /// Generates a Hanning window using SSE acceleration.
        /// </summary>
        /// <param name="size">The size of the Hanning window.</param>
        /// <returns>The Hanning window array.</returns>
        private static unsafe float[] HanningWindowSse(int size)
        {
            float[] window = new float[size];
            int vectorSize = 4;
            int remainder = size % vectorSize;

            fixed (float* pWindow = window)
            {
                v128 vConstA = new v128(0.5f);
                v128 vConstB = new v128(0.5f);
                v128 vTwoPi = new v128(2.0f * MathF.PI / (size - 1));

                for (int i = 0; i < size - remainder; i += vectorSize)
                {
                    v128 vIndices = new v128(i, i + 1, i + 2, i + 3);
                    v128 vCosArg = X86.Sse.mul_ps(vTwoPi, vIndices);
                    v128 vCos = FastCosineSse(vCosArg);
                    v128 vResult = X86.Sse.sub_ps(vConstA, X86.Sse.mul_ps(vConstB, vCos));

                    X86.Sse.storeu_ps(pWindow + i, vResult);
                }
            }

            // 处理剩余元素
            for (int i = size - remainder; i < size; i++)
            {
                window[i] = 0.5f * (1.0f - MathF.Cos(2 * MathF.PI * i / (size - 1)));
            }

            return window;
        }

        /// <summary>
        /// Generates a Hanning window using AVX acceleration.
        /// </summary>
        /// <param name="size">The size of the Hanning window.</param>
        /// <returns>The Hanning window array.</returns>
        private static unsafe float[] HanningWindowAvx(int size)
        {
            float[] window = new float[size];
            int vectorSize = 8;
            int remainder = size % vectorSize;

            fixed (float* pWindow = window)
            {
                v256 vConstA = new v256(0.5f);
                v256 vConstB = new v256(0.5f);
                v256 vTwoPi = new v256(2.0f * MathF.PI / (size - 1));

                for (int i = 0; i < size - remainder; i += vectorSize)
                {
                    v256 vIndices = new v256(
                        i, i + 1, i + 2, i + 3,
                        i + 4, i + 5, i + 6, i + 7
                    );

                    v256 vCosArg = X86.Avx.mm256_mul_ps(vTwoPi, vIndices);
                    v256 vCos = FastCosineAvx(vCosArg);
                    v256 vResult = X86.Avx.mm256_sub_ps(vConstA, X86.Avx.mm256_mul_ps(vConstB, vCos));

                    X86.Avx.mm256_storeu_ps(pWindow + i, vResult);
                }
            }

            // 处理剩余元素
            for (int i = size - remainder; i < size; i++)
            {
                window[i] = 0.5f * (1.0f - MathF.Cos(2 * MathF.PI * i / (size - 1)));
            }

            return window;
        }

        /// <summary>
        /// Performs linear interpolation between two values
        /// </summary>
        public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0, 1);

        /// <summary>
        /// Checks if a number is a power of two (2, 4, 8, 16, etc.).
        /// </summary>
        /// <param name="n">The number to check</param>
        /// <returns></returns>
        public static bool IsPowerOfTwo(int n) => (n & (n - 1)) == 0 && n != 0;

        /// <summary>
        /// Returns the remainder after division, in the range [-0.5, 0.5).
        /// </summary>
        public static double Mod(this double x, double y) => x - y * Math.Floor(x / y);

        /// <summary>
        /// Returns the principal angle of a number in the range [-PI, PI).
        /// </summary>
        public static float PrincipalAngle(float angle)
        {
            // Returns angle in range [-PI, PI)
            return angle - (2 * MathF.PI * MathF.Floor((angle + MathF.PI) / (2 * MathF.PI)));
        }

        /// <summary>
        /// Approximates the cosine of a vector using SSE instructions.
        /// Placeholder for now, I need to implement a more accurate approximation.
        /// </summary>
        /// <param name="x">The input vector.</param>
        /// <returns>The approximated cosine of the input vector.</returns>
        private static v128 FastCosineSse(v128 x)
        {
            v128 x2 = X86.Sse.mul_ps(x, x);
            v128 x4 = X86.Sse.mul_ps(x2, x2);
            v128 term2 = X86.Sse.mul_ps(x2, new v128(1f / 2f));
            v128 term4 = X86.Sse.mul_ps(x4, new v128(1f / 24f));
            return X86.Sse.sub_ps(new v128(1.0f), X86.Sse.add_ps(term2, term4));
        }

        /// <summary>
        /// Approximates the cosine of a vector using AVX instructions.
        /// Placeholder for now, I need to implement a more accurate approximation.
        /// </summary>
        /// <param name="x">The input vector.</param>
        /// <returns>The approximated cosine of the input vector.</returns>
        private static v256 FastCosineAvx(v256 x)
        {
            v256 x2 = X86.Avx.mm256_mul_ps(x, x);
            v256 x4 = X86.Avx.mm256_mul_ps(x2, x2);
            v256 term2 = X86.Avx.mm256_mul_ps(x2, new v256(1f / 2f));
            v256 term4 = X86.Avx.mm256_mul_ps(x4, new v256(1f / 24f));
            return X86.Avx.mm256_sub_ps(new v256(1.0f), X86.Avx.mm256_add_ps(term2, term4));
        }
    }
}