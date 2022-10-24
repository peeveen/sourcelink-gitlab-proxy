using System.Text;

namespace SourceLinkGitLabProxy;

public class EncodingUtils {
	public static IDictionary<string, Encoding> Encodings = new Dictionary<string, Encoding> {
		["utf-32BE"] = new UTF32Encoding(true, true),
		["utf-32"] = new UTF32Encoding(false, true),
		["utf-16BE"] = new UnicodeEncoding(true, true),
		["utf-16"] = new UnicodeEncoding(false, true),
		["utf-8"] = new UTF8Encoding(true)
	};

	// This code was adapted from https://stackoverflow.com/questions/1025332/determine-a-strings-encoding-in-c-sharp
	record EncodingSignature(byte[] Signature, Encoding Encoding) {
		internal bool Matches(byte[] bytes) =>
			bytes.Length >= Signature.Length ? Signature.SequenceEqual(bytes[0..Signature.Length]) : false;
	}
	static readonly EncodingSignature[] signatures = new[]{
		new EncodingSignature(new byte[]{0x00, 0x00, 0xFE, 0xFF}, Encodings["utf-32BE"]),
		new EncodingSignature(new byte[]{0xFF, 0xFE, 0x00, 0x00}, Encodings["utf-32"]),
		new EncodingSignature(new byte[]{0xFE, 0xFF}, Encodings["utf-16BE"]),
		new EncodingSignature(new byte[]{0xFF, 0xFE}, Encodings["utf-16"]),
		new EncodingSignature(new byte[]{0xEF, 0xBB, 0xBF}, Encodings["utf-8"]),
	};
	// If none of the above encodings are detected, fall back to UTF-8 (no BOM)
	static readonly EncodingSignature FallbackEncodingSignature = new EncodingSignature(new byte[0], new UTF8Encoding(false));

	static EncodingSignature DetermineStringEncoding(byte[] bytes) =>
		signatures.FirstOrDefault(signature => signature.Matches(bytes)) ?? FallbackEncodingSignature;

	public static (Encoding, string) GetFileContentAsString(byte[] fileContent) {
		var encodingSignature = DetermineStringEncoding(fileContent);
		var decodedString = encodingSignature.Encoding.GetString(fileContent[encodingSignature.Signature.Length..]);
		return (encodingSignature.Encoding, decodedString);
	}

	public static byte[] GetFileContentForString(Encoding encoding, string fileString) {
		var preamble = encoding.GetPreamble();
		var stringBytes = encoding.GetBytes(fileString);
		var combinedBytes = new byte[preamble.Length + stringBytes.Length];
		Array.Copy(preamble, combinedBytes, preamble.Length);
		Array.Copy(stringBytes, 0, combinedBytes, preamble.Length, stringBytes.Length);
		return combinedBytes;
	}
}