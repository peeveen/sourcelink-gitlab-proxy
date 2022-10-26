using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SourceLinkGitLabProxy.Test;

[TestClass]
public class URLTests {
	[TestMethod]
	public void TestURLParse() {
		var projectPath = "steven.frew/someproject";
		var commitHash = "09ef7F892345";
		var filePath = "blah/yap/folder/file.ext";
		var parseResult = new GitLabSourceFileRequest($"/{projectPath}/raw/{commitHash}/{filePath}");
		Assert.AreEqual(projectPath, parseResult.ProjectPath);
		Assert.AreEqual(commitHash, parseResult.CommitHash);
		Assert.AreEqual(filePath, parseResult.FilePath);
	}
}