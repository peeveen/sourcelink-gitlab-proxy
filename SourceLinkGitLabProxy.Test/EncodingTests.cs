using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SourceLinkGitLabProxy.Test;

[TestClass]
public class EncodingTests {
	[TestMethod]
	public async Task TestEncodingDetection() {
		foreach (var path in Directory.GetFiles(Path.Join("..", "..", "..", "testFiles"))) {
			var fileContent = await File.ReadAllBytesAsync(path);
			var (encoding, stringContent) = EncodingUtils.GetFileContentAsString(fileContent);
			var expectedEncoding = stringContent.ReplaceLineEndings().Split(Environment.NewLine).FirstOrDefault();
			Assert.AreEqual(expectedEncoding, encoding.WebName);
		}
	}
}