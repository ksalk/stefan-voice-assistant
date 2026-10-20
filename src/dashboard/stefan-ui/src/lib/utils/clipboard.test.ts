import { describe, expect, it, afterEach, vi } from 'vitest';
import { copyTextToClipboard } from './clipboard';

type FakeClipboard = { writeText: ReturnType<typeof vi.fn> };

function stubClipboard(clipboard: FakeClipboard | undefined) {
	vi.stubGlobal('navigator', { clipboard });
}

function stubDocument(execCommand: (command: string) => boolean) {
	const textarea = {
		value: '',
		style: {} as Record<string, string>,
		setAttribute: vi.fn(),
		select: vi.fn()
	};
	const body = {
		appendChild: vi.fn(),
		removeChild: vi.fn()
	};
	vi.stubGlobal('document', {
		createElement: vi.fn(() => textarea),
		body,
		execCommand: vi.fn(execCommand)
	});
	return { textarea, body };
}

describe('copyTextToClipboard', () => {
	afterEach(() => {
		vi.unstubAllGlobals();
	});

	it('writes the text with the clipboard API when it is available', async () => {
		const clipboard: FakeClipboard = { writeText: vi.fn().mockResolvedValue(undefined) };
		stubClipboard(clipboard);

		await copyTextToClipboard('line 1\nline 2');

		expect(clipboard.writeText).toHaveBeenCalledWith('line 1\nline 2');
	});

	it('falls back to the command path when the clipboard API is missing', async () => {
		stubClipboard(undefined);
		const { textarea, body } = stubDocument(() => true);

		await copyTextToClipboard('fallback text');

		expect(textarea.value).toBe('fallback text');
		expect(textarea.select).toHaveBeenCalled();
		expect(body.appendChild).toHaveBeenCalled();
		expect(body.removeChild).toHaveBeenCalled();
	});

	it('reports an error when no copy path works', async () => {
		stubClipboard(undefined);
		stubDocument(() => false);

		await expect(copyTextToClipboard('text')).rejects.toThrow(/Clipboard is not available/);
	});

	it('reports the error from the clipboard API', async () => {
		const clipboard: FakeClipboard = {
			writeText: vi.fn().mockRejectedValue(new Error('Document is not focused'))
		};
		stubClipboard(clipboard);

		await expect(copyTextToClipboard('text')).rejects.toThrow('Document is not focused');
	});

	it('falls back to the command path when the clipboard API rejects', async () => {
		const clipboard: FakeClipboard = {
			writeText: vi.fn().mockRejectedValue(new Error('NotAllowedError'))
		};
		stubClipboard(clipboard);
		const { textarea } = stubDocument(() => true);

		await copyTextToClipboard('fallback text');

		expect(textarea.value).toBe('fallback text');
	});
});
