using System;

namespace SmallMind.Core.Rng
{
    /// <summary>
    /// Deterministic random number generator using XorShift128 algorithm.
    /// Provides reproducible random sequences when seeded with the same value.
    /// Pure C# implementation with no external dependencies.
    /// </summary>
    internal sealed class DeterministicRng
    {
        private ulong _state0;
        private ulong _state1;

        /// <summary>
        /// Initialize RNG with a seed value.
        /// Same seed will produce identical sequences.
        /// </summary>
        /// <param name="seed">Seed value for initialization</param>
        public DeterministicRng(int seed)
        {
            // SplitMix64 initialization to avoid weak initial states
            ulong s = (ulong)seed;
            _state0 = SplitMix64(ref s);
            _state1 = SplitMix64(ref s);
            
            // Ensure neither state is zero (degenerate case)
            if (_state0 == 0 && _state1 == 0)
            {
                _state0 = 1;
                _state1 = 1;
            }
        }

        /// <summary>
        /// Generate next unsigned 64-bit random number.
        /// </summary>
        public ulong NextULong()
        {
            // XorShift128 algorithm
            ulong s0 = _state0;
            ulong s1 = _state1;
            ulong result = s0 + s1;

            s1 ^= s0;
            _state0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
            _state1 = RotateLeft(s1, 37);

            return result;
        }

        /// <summary>
        /// Generate random double in range [0.0, 1.0).
        /// </summary>
        public double NextDouble()
        {
            // Use upper 53 bits for precision (double has 53-bit mantissa)
            const ulong mask53 = (1UL << 53) - 1;
            ulong bits = NextULong() & mask53;
            return bits * (1.0 / (1UL << 53));
        }

        /// <summary>
        /// Generate random float in range [0.0, 1.0).
        /// </summary>
        public float NextFloat()
        {
            // Use upper 24 bits for precision (float has 24-bit mantissa)
            const uint mask24 = (1U << 24) - 1;
            uint bits = (uint)(NextULong() >> 40) & mask24;
            return bits * (1.0f / (1U << 24));
        }

        /// <summary>
        /// Generate random integer in range [0, maxValue).
        /// </summary>
        public int Next(int maxValue)
        {
            if (maxValue <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxValue), "maxValue must be positive");
            }

            // Use rejection sampling to avoid modulo bias
            ulong range = (ulong)maxValue;
            ulong limit = ulong.MaxValue - (ulong.MaxValue % range);
            
            ulong sample;
            do
            {
                sample = NextULong();
            } while (sample >= limit);

            return (int)(sample % range);
        }

        /// <summary>
        /// Generate random integer in range [minValue, maxValue).
        /// </summary>
        public int Next(int minValue, int maxValue)
        {
            if (maxValue <= minValue)
            {
                throw new ArgumentOutOfRangeException(nameof(maxValue), "maxValue must be greater than minValue");
            }

            long range = (long)maxValue - minValue;
            return minValue + Next((int)range);
        }

        private static ulong RotateLeft(ulong x, int k)
        {
            return (x << k) | (x >> (64 - k));
        }

        private static ulong SplitMix64(ref ulong state)
        {
            // SplitMix64 hash function for seed initialization
            state += 0x9e3779b97f4a7c15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
            z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
            return z ^ (z >> 31);
        }
    }
}
