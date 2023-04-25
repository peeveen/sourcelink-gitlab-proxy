using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SourceLinkGitLabProxy.Test;

[TestClass]
public class URLTests {
	[TestMethod]
	public void TestURLParse() {
		var projectPath = "steven.frew/someproject";
		var commitHash = "09ef7F892345";
		var filePath = "blah/yap/folder/file.ext";
		var parseResult1 = new GitLabSourceFileRequest($"/{projectPath}/raw/{commitHash}/{filePath}");
		var parseResult2 = new GitLabSourceFileRequest($"/{projectPath}/-/raw/{commitHash}/{filePath}");
		Assert.AreEqual(projectPath, parseResult1.ProjectPath);
		Assert.AreEqual(commitHash, parseResult1.CommitHash);
		Assert.AreEqual(filePath, parseResult1.FilePath);
		Assert.AreEqual(projectPath, parseResult2.ProjectPath);
		Assert.AreEqual(commitHash, parseResult2.CommitHash);
		Assert.AreEqual(filePath, parseResult2.FilePath);
	}
}