using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SmallMind.Tokenizers;

/// <summary>
/// High-performance merge engine for Byte Pair Encoding.
/// Optimized for CPU-only execution with minimal allocations.
/// </summary>
public static class BpeMergeEngine
{
    /// <summary>
    /// Counts pair frequencies in token sequences.
    /// Uses a long-based key encoding to avoid tuple allocations.
    /// </summary>
    public readonly struct PairCounter
    {
        private readonly Dictionary<long, int> _pairCounts;

        public PairCounter()
        {
            _pairCounts = new Dictionary<long, int>();
        }

        /// <summary>
        /// Pack two int token IDs into a single long key.
        /// This avoids tuple allocations and provides better cache locality.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long PackPair(int tokenA, int tokenB)
        {
            return ((long)tokenA << 32) | (uint)tokenB;
        }

        /// <summary>
        /// Unpack a long key back into two int token IDs.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (int, int) UnpackPair(long packedPair)
        {
            int tokenA = (int)(packedPair >> 32);
            int tokenB = (int)(packedPair & 0xFFFFFFFF);
            return (tokenA, tokenB);
        }

        /// <summary>
        /// Count all adjacent pairs in the token sequence.
        /// </summary>
        public void CountPairs(ReadOnlySpan<int> tokens, Dictionary<long, int> pairCounts)
        {
            for (int i = 0; i < tokens.Length - 1; i++)
            {
                long pair = PackPair(tokens[i], tokens[i + 1]);
                pairCounts.TryGetValue(pair, out int count);
                pairCounts[pair] = count + 1;
            }
        }

        /// <summary>
        /// Update pair counts after a merge at a specific position.
        /// Only updates the pairs that were affected by the merge (incremental update).
        /// </summary>
        public static void UpdatePairCountsAfterMerge(
            ReadOnlySpan<int> tokens,
            int mergePosition,
            Dictionary<long, int> pairCounts)
        {
            // Decrement counts for the old pairs that were destroyed
            if (mergePosition > 0)
            {
                // Pair before merge position was destroyed
                long oldPair = PackPair(tokens[mergePosition - 1], tokens[mergePosition]);
                if (pairCounts.TryGetValue(oldPair, out int count))
                {
                    if (count > 1)
                        pairCounts[oldPair] = count - 1;
                    else
                        pairCounts.Remove(oldPair);
                }
            }

            if (mergePosition + 2 < tokens.Length)
            {
                // Pair after merge position was destroyed
                long oldPair = PackPair(tokens[mergePosition + 1], tokens[mergePosition + 2]);
                if (pairCounts.TryGetValue(oldPair, out int count))
                {
                    if (count > 1)
                        pairCounts[oldPair] = count - 1;
                    else
                        pairCounts.Remove(oldPair);
                }
            }

            // Increment counts for the new pairs that were created
            // The merge creates a new token at mergePosition, so we need to add pairs with neighbors
            if (mergePosition > 0)
            {
                // New pair with left neighbor
                long newPair = PackPair(tokens[mergePosition - 1], tokens[mergePosition]);
                pairCounts.TryGetValue(newPair, out int count);
                pairCounts[newPair] = count + 1;
            }

            if (mergePosition + 1 < tokens.Length)
            {
                // New pair with right neighbor
                long newPair = PackPair(tokens[mergePosition], tokens[mergePosition + 1]);
                pairCounts.TryGetValue(newPair, out int count);
                pairCounts[newPair] = count + 1;
            }
        }

        /// <summary>
        /// Find the most frequent pair from pair counts.
        /// </summary>
        public static (int tokenA, int tokenB, int frequency) FindMostFrequentPair(
            Dictionary<long, int> pairCounts)
        {
            if (pairCounts.Count == 0)
                return (-1, -1, 0);

            long bestPair = 0;
            int maxCount = 0;

            foreach (var kvp in pairCounts)
            {
                if (kvp.Value > maxCount)
                {
                    bestPair = kvp.Key;
                    maxCount = kvp.Value;
                }
            }

            var (tokenA, tokenB) = UnpackPair(bestPair);
            return (tokenA, tokenB, maxCount);
        }

        /// <summary>
        /// Pack a pair into a long key (public helper).
        /// </summary>
        public static long Pack(int tokenA, int tokenB) => PackPair(tokenA, tokenB);

        /// <summary>
        /// Unpack a long key into a pair (public helper).
        /// </summary>
        public static (int, int) Unpack(long packedPair) => UnpackPair(packedPair);
    }

    /// <summary>
    /// Applies merge rules to token sequences in-place with minimal allocations.
    /// </summary>
    public sealed class MergeApplicator
    {
        /// <summary>
        /// Apply a single merge rule to a token sequence.
        /// Scans left-to-right and replaces all occurrences of (tokenA, tokenB) with newTokenId.
        /// Returns the new length of the sequence.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ApplyMerge(
            Span<int> tokens,
            int currentLength,
            int tokenA,
            int tokenB,
            int newTokenId)
        {
            int writePos = 0;
            int readPos = 0;

            while (readPos < currentLength)
            {
                if (readPos < currentLength - 1 &&
                    tokens[readPos] == tokenA &&
                    tokens[readPos + 1] == tokenB)
                {
                    // Found a match - replace with merged token
                    tokens[writePos++] = newTokenId;
                    readPos += 2; // Skip both tokens
                }
                else
                {
                    // No match - copy token as-is
                    tokens[writePos++] = tokens[readPos++];
                }
            }

            return writePos; // New length
        }

        /// <summary>
        /// Apply multiple merges in priority order to a token sequence.
        /// Uses a dictionary for O(1) merge lookup.
        /// Returns the final token count.
        /// </summary>
        public static int ApplyMergesWithPriority(
            Span<int> tokens,
            int initialLength,
            IReadOnlyList<(int tokenA, int tokenB)> merges,
            Dictionary<(int, int), int> mergeToNewToken)
        {
            int currentLength = initialLength;

            // Apply merges in priority order (first merge has highest priority)
            foreach (var merge in merges)
            {
                if (currentLength <= 1)
                    break;

                if (mergeToNewToken.TryGetValue(merge, out int newTokenId))
                {
                    currentLength = ApplyMerge(
                        tokens,
                        currentLength,
                        merge.tokenA,
                        merge.tokenB,
                        newTokenId);
                }
            }

            return currentLength;
        }

        /// <summary>
        /// Apply merges to a token sequence using an iterative approach.
        /// Finds and applies the highest-priority merge, then repeats until no more merges can be applied.
        /// This is the standard BPE encoding algorithm.
        /// </summary>
        public static int ApplyMergesIterative(
            Span<int> tokens,
            int initialLength,
            Dictionary<(int, int), int> mergePriorities)
        {
            int currentLength = initialLength;

            while (currentLength > 1)
            {
                // Find the highest-priority merge in current sequence
                int bestPriority = int.MaxValue;
                int bestPos = -1;
                (int, int) bestPair = (-1, -1);

                for (int i = 0; i < currentLength - 1; i++)
                {
                    var pair = (tokens[i], tokens[i + 1]);
                    if (mergePriorities.TryGetValue(pair, out int priority) && priority < bestPriority)
                    {
                        bestPriority = priority;
                        bestPos = i;
                        bestPair = pair;
                    }
                }

                if (bestPos == -1)
                    break; // No more merges can be applied

                // Apply the merge: replace pair at bestPos with a single token
                // The new token ID is derived from the merge priority
                // For encoding, we need to look up what the merged token actually is
                // This requires having the vocabulary available
                // For now, we'll just mark it with a placeholder
                // The actual implementation needs access to the vocab to get the merged token ID

                // Shift tokens left to remove the second token of the pair
                for (int i = bestPos + 1; i < currentLength - 1; i++)
                {
                    tokens[i] = tokens[i + 1];
                }
                currentLength--;

                // Note: In practice, we need to store the actual merged token ID
                // This is a simplified version - the real implementation in ByteLevelBpeTokenizer
                // will handle the full merge logic with vocabulary lookups
            }

            return currentLength;
        }
    }
}
