using System.Runtime.CompilerServices;

// Allow test projects to access internal types
[assembly: InternalsVisibleTo("SmallMind.ModelRegistry.Tests")]
[assembly: InternalsVisibleTo("SmallMind.Tests")]
[assembly: InternalsVisibleTo("SmallMind.IntegrationTests")]

// Allow tool projects to access internal types
[assembly: InternalsVisibleTo("SmallMind.Console")]
[assembly: InternalsVisibleTo("SmallMind.Server")]
