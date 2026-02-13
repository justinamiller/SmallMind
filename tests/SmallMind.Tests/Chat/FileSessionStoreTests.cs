using SmallMind.Abstractions;
using SmallMind.Engine;

namespace SmallMind.Tests.Chat
{
    /// <summary>
    /// Tests for FileSessionStore ensuring persistence and schema versioning.
    /// </summary>
    public class FileSessionStoreTests : IDisposable
    {
        private readonly string _testDirectory;

        public FileSessionStoreTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"smallmind_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        [Fact]
        public async Task FileSessionStore_UpsertAndGet_RoundTrip()
        {
            var store = new FileSessionStore(_testDirectory);
            var session = new ChatSessionData
            {
                SessionId = "test-123",
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                Metadata = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["key1"] = "value1",
                    ["key2"] = "value2"
                }
            };

            session.Turns.Add(new ChatTurnData
            {
                UserMessage = "Hello",
                AssistantMessage = "Hi there!",
                Timestamp = DateTime.UtcNow
            });

            await store.UpsertAsync(session);
            var retrieved = await store.GetAsync("test-123");

            Assert.NotNull(retrieved);
            Assert.Equal("test-123", retrieved!.SessionId);
            Assert.Single(retrieved.Turns);
            Assert.Equal("Hello", retrieved.Turns[0].UserMessage);
            Assert.Equal("Hi there!", retrieved.Turns[0].AssistantMessage);
            Assert.Equal(2, retrieved.Metadata.Count);
        }

        [Fact]
        public async Task FileSessionStore_GetNonExistent_ReturnsNull()
        {
            var store = new FileSessionStore(_testDirectory);
            var result = await store.GetAsync("non-existent");

            Assert.Null(result);
        }

        [Fact]
        public async Task FileSessionStore_DeleteSession_RemovesFile()
        {
            var store = new FileSessionStore(_testDirectory);
            var session = new ChatSessionData
            {
                SessionId = "delete-me",
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            await store.UpsertAsync(session);
            Assert.True(await store.ExistsAsync("delete-me"));

            await store.DeleteAsync("delete-me");
            Assert.False(await store.ExistsAsync("delete-me"));
        }

        [Fact]
        public async Task FileSessionStore_Exists_ChecksCorrectly()
        {
            var store = new FileSessionStore(_testDirectory);

            Assert.False(await store.ExistsAsync("not-created"));

            var session = new ChatSessionData
            {
                SessionId = "exists-test",
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            await store.UpsertAsync(session);
            Assert.True(await store.ExistsAsync("exists-test"));
        }

        [Fact]
        public async Task FileSessionStore_UpdateExisting_Overwrites()
        {
            var store = new FileSessionStore(_testDirectory);
            var session = new ChatSessionData
            {
                SessionId = "update-test",
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            session.Turns.Add(new ChatTurnData
            {
                UserMessage = "First",
                AssistantMessage = "Response 1",
                Timestamp = DateTime.UtcNow
            });

            await store.UpsertAsync(session);

            // Update with new turn
            session.Turns.Add(new ChatTurnData
            {
                UserMessage = "Second",
                AssistantMessage = "Response 2",
                Timestamp = DateTime.UtcNow
            });

            await store.UpsertAsync(session);

            var retrieved = await store.GetAsync("update-test");
            Assert.NotNull(retrieved);
            Assert.Equal(2, retrieved!.Turns.Count);
            Assert.Equal("Second", retrieved.Turns[1].UserMessage);
        }

        [Fact]
        public async Task FileSessionStore_MultipleStorages_Independent()
        {
            var dir1 = Path.Combine(_testDirectory, "store1");
            var dir2 = Path.Combine(_testDirectory, "store2");

            var store1 = new FileSessionStore(dir1);
            var store2 = new FileSessionStore(dir2);

            var session = new ChatSessionData
            {
                SessionId = "shared-id",
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            await store1.UpsertAsync(session);

            Assert.True(await store1.ExistsAsync("shared-id"));
            Assert.False(await store2.ExistsAsync("shared-id"));
        }

        [Fact]
        public async Task FileSessionStore_InvalidSessionId_SanitizesFileName()
        {
            var store = new FileSessionStore(_testDirectory);
            var session = new ChatSessionData
            {
                SessionId = "test/with\\invalid:chars",
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            // Should not throw
            await store.UpsertAsync(session);
            var retrieved = await store.GetAsync("test/with\\invalid:chars");

            Assert.NotNull(retrieved);
            Assert.Equal("test/with\\invalid:chars", retrieved!.SessionId);
        }

        [Fact]
        public async Task FileSessionStore_CitationsInTurn_Persisted()
        {
            var store = new FileSessionStore(_testDirectory);
            var session = new ChatSessionData
            {
                SessionId = "citations-test",
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            session.Turns.Add(new ChatTurnData
            {
                UserMessage = "What is AI?",
                AssistantMessage = "AI is artificial intelligence.",
                Timestamp = DateTime.UtcNow,
                Citations = new System.Collections.Generic.List<string> { "source1.txt", "source2.pdf" }
            });

            await store.UpsertAsync(session);
            var retrieved = await store.GetAsync("citations-test");

            Assert.NotNull(retrieved);
            Assert.Single(retrieved!.Turns);
            Assert.Equal(2, retrieved.Turns[0].Citations.Count);
            Assert.Contains("source1.txt", retrieved.Turns[0].Citations);
        }

        [Fact]
        public async Task FileSessionStore_MetadataInTurn_Persisted()
        {
            var store = new FileSessionStore(_testDirectory);
            var session = new ChatSessionData
            {
                SessionId = "metadata-test",
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            session.Turns.Add(new ChatTurnData
            {
                UserMessage = "Test",
                AssistantMessage = "Response",
                Timestamp = DateTime.UtcNow,
                Metadata = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["model"] = "gpt-test",
                    ["version"] = "1.0"
                }
            });

            await store.UpsertAsync(session);
            var retrieved = await store.GetAsync("metadata-test");

            Assert.NotNull(retrieved);
            Assert.Single(retrieved!.Turns);
            Assert.Equal(2, retrieved.Turns[0].Metadata.Count);
            Assert.Equal("gpt-test", retrieved.Turns[0].Metadata["model"]);
        }
    }
}
