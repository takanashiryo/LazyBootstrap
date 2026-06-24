using System.Text;

namespace LazyBootstrap.Features.Launch.Services
{
    public static class SpiceLogEncoding
    {
        private static readonly Encoding ShiftJisEncoding = CreateShiftJisEncoding();

        public static Encoding ShiftJis => ShiftJisEncoding;

        private static Encoding CreateShiftJisEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(932);
        }
    }
}
