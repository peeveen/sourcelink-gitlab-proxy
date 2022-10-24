namespace SourceLinkGitLabProxy;

// How should the post-processor handle the received source code? Git will (by default) store line
// endings in Unix format (LF only). We *probably* want to switch them to Windows format (CRLF),
// but the proxy can be configured to use either.
public enum LineEndingChange {
	Windows, Unix, None
}