using System.Text;

namespace SourceLinkGitLabProxy;

public class EncodingUtils {
	public static IEnumerable<Encoding> Encodings = new List<Encoding>{
		new UTF32Encoding(true, true), // UTF32 Big Endian
		new UTF32Encoding(false, true), // UTF32 Little Endian
		new UnicodeEncoding(true, true), // UTF16 Big Endian
		new UnicodeEncoding(false, true), // UTF16 Little Endian
		new UTF8Encoding(true) // UTF8
	};

	record EncodingSignature(byte[] Signature, Encoding Encoding) {
		internal bool Matches(byte[] bytes) =>
			bytes.Length >= Signature.Length ? Signature.SequenceEqual(bytes[0..Signature.Length]) : false;
	}

	static readonly IEnumerable<EncodingSignature> EncodingSignatures = Encodings.Select(encoding => new EncodingSignature(encoding.GetPreamble(), encoding));

	// If none of the encoding signatures are detected, fall back to UTF-8 (no BOM)
	static readonly EncodingSignature FallbackEncodingSignature = new EncodingSignature(new byte[0], new UTF8Encoding(false));

	static EncodingSignature DetermineStringEncoding(byte[] bytes) =>
		EncodingSignatures.FirstOrDefault(signature => signature.Matches(bytes)) ?? FallbackEncodingSignature;

	// Returns the given file content as a string, and the encoding that we THINK was used within it.
	public static (Encoding, string) GetFileContentAsString(byte[] fileContent) {
		var encodingSignature = DetermineStringEncoding(fileContent);
		var decodedString = encodingSignature.Encoding.GetString(fileContent[encodingSignature.Signature.Length..]);
		return (encodingSignature.Encoding, decodedString);
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