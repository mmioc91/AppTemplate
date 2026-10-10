using System.Runtime.CompilerServices;

// Persistence tests seed and read the database directly; nothing else may use AppDbContext.
[assembly: InternalsVisibleTo("AppTemplate.Integration.Tests")]
