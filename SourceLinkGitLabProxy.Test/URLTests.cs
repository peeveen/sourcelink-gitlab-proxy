using Microsoft.VisualStudio.TestTools.UnitTesting;
using SourceLinkGitLabProxy.Controllers;

namespace SourceLinkGitLabProxy.Test;

[TestClass]
public class URLTests {
	[TestMethod]
	public void TestURLParse() {
		var projectPath = "steven.frew/someproject";
		var commitHash = "09ef7F892345";
		var filePath = "blah/yap/folder/file.ext";
		var parseResult = GitLabController.ParseURL($"/{projectPath}/raw/{commitHash}/{filePath}");
		Assert.AreEqual(projectPath, parseResult.projectPath);
		Assert.AreEqual(commitHash, parseResult.commitHash);
		Assert.AreEqual(filePath, parseResult.filePath);
	}
}