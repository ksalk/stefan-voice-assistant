/**
 * Copies text to the clipboard.
 *
 * The async clipboard API exists only in secure contexts. The dashboard runs
 * over plain HTTP on a LAN address, so `navigator.clipboard` is missing there.
 * The helper falls back to the legacy command path and reports a clear error
 * when neither path works.
 */
export async function copyTextToClipboard(text: string): Promise<void> {
	if (typeof navigator !== 'undefined' && typeof navigator.clipboard?.writeText === 'function') {
		try {
			await navigator.clipboard.writeText(text);
			return;
		} catch (error) {
			if (copyWithExecCommand(text)) {
				return;
			}

			throw error;
		}
	}

	if (copyWithExecCommand(text)) {
		return;
	}

	throw new Error(
		'Clipboard is not available. Serve the dashboard over HTTPS or open it on localhost.'
	);
}

function copyWithExecCommand(text: string): boolean {
	if (typeof document === 'undefined' || typeof document.execCommand !== 'function') {
		return false;
	}

	const textarea = document.createElement('textarea');
	textarea.value = text;
	textarea.setAttribute('readonly', '');
	textarea.style.position = 'fixed';
	textarea.style.top = '0';
	textarea.style.opacity = '0';
	document.body.appendChild(textarea);
	textarea.select();

	try {
		return document.execCommand('copy');
	} catch {
		return false;
	} finally {
		document.body.removeChild(textarea);
	}
}
