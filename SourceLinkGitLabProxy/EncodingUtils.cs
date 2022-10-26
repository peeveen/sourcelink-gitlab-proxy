using System.Text;

namespace SourceLinkGitLabProxy;

static class EncodingExtensions {
	internal static bool PreambleMatches(this Encoding encoding, byte[] bytes) =>
		bytes.Length >= encoding.Preamble.Length && encoding.Preamble.SequenceEqual(bytes[0..encoding.Preamble.Length]);
}

public class EncodingUtils {
	public static IEnumerable<Encoding> Encodings = new List<Encoding>{
		new UTF32Encoding(true, true), // UTF32 Big Endian
		new UTF32Encoding(false, true), // UTF32 Little Endian
		new UnicodeEncoding(true, true), // UTF16 Big Endian
		new UnicodeEncoding(false, true), // UTF16 Little Endian
		new UTF8Encoding(true) // UTF8
	};

	// If none of the encoding signatures are detected, fall back to UTF-8 (no BOM)
	static readonly Encoding FallbackEncodingSignature = new UTF8Encoding(false);

	static Encoding DetermineStringEncoding(byte[] bytes) =>
		Encodings.FirstOrDefault(encoding => encoding.PreambleMatches(bytes)) ?? FallbackEncodingSignature;

	// Returns the given file content as a string, and the encoding that we THINK was used within it.
	public static (Encoding, string) GetFileContentAsString(byte[] fileContent) {
		var encoding = DetermineStringEncoding(fileContent);
		var decodedString = encoding.GetString(fileContent[encoding.Preamble.Length..]);
		return (encoding, decodedString);
	}

	// For the given string and encoding, return the file content to write out, including encoding preamble.
	public static byte[] GetFileContentForString(Encoding encoding, string fileString) {
		var preamble = encoding.GetPreamble();
		var stringBytes = encoding.GetBytes(fileString);
		var combinedBytes = new byte[preamble.Length + stringBytes.Length];
		Array.Copy(preamble, combinedBytes, preamble.Length);
		Array.Copy(stringBytes, 0, combinedBytes, preamble.Length, stringBytes.Length);
		return combinedBytes;
	}
}