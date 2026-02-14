using System.Runtime.CompilerServices;

// Allow test projects to access internal types
[assembly: InternalsVisibleTo("SmallMind.Tests")]
[assembly: InternalsVisibleTo("SmallMind.IntegrationTests")]
[assembly: InternalsVisibleTo("SmallMind.PerfTests")]

// Allow Console project to access internal types
[assembly: InternalsVisibleTo("SmallMind.Console")]

// Allow example projects to access internal types for educational purposes
[assembly: InternalsVisibleTo("ChatLevel3Examples")]
[assembly: InternalsVisibleTo("GoldenPath")]
[assembly: InternalsVisibleTo("KVCacheExample")]
[assembly: InternalsVisibleTo("CachingAndBatching")]
