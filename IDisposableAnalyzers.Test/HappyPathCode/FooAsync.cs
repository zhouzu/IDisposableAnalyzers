// ReSharper disable UnusedMember.Global Used in HappyPathWithAll.PropertyChangedAnalyzersSln
// ReSharper disable NotAccessedField.Local
// ReSharper disable UnusedVariable
// ReSharper disable UnusedParameter.Local
namespace IDisposableAnalyzers.Test.HappyPathCode
{
    using System.IO;
    using System.Threading.Tasks;

    public static class FooAsync
    {
        public static async Task<string> Bar1Async()
        {
            using (var stream = await ReadAsync(string.Empty))
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadLine();
                }
            }
        }

        public static async Task<string> Bar2Async()
        {
            using (var stream = await ReadAsync(string.Empty).ConfigureAwait(false))
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadLine();
                }
            }
        }

        private static async Task<Stream> ReadAsync(this string fileName)
        {
            var stream = new MemoryStream();
            using (var fileStream = File.OpenRead(fileName))
            {
                await fileStream.CopyToAsync(stream)
                                .ConfigureAwait(false);
            }

            stream.Position = 0;
            return stream;
        }
    }
}
