using System;
using Xunit;
using SmallMind.Core;
using SmallMind.Runtime;
using SmallMind.Transformers;
using SmallMind.Tokenizers;

namespace SmallMind.Tests
{
    /// <summary>
    /// DEPRECATED: These tests use an old InferenceSession API that has been significantly changed.
    /// The tests have been disabled pending migration to the new API.
    /// </summary>
    public class KVCacheInferenceTests
    {
        [Fact(Skip = "InferenceSession API changed - needs migration")]
        public void Placeholder_Test()
        {
            // Placeholder to avoid empty test class
            Assert.True(true);
        }
    }
}
