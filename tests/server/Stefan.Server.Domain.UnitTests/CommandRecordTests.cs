namespace Stefan.Server.Domain.UnitTests;

public class CommandRecordTests
{
    [Fact]
    public void SaveTranscriptionResult_SetsTranscriptAndDurationAndStatus()
    {
        var record = new CommandRecord();

        record.SaveTranscriptionResult("hello world", 150.5);

        Assert.Equal("hello world", record.Transcript);
        Assert.Equal(150.5, record.SttDurationMs);
        Assert.Equal(CommandStatus.SttSuccess, record.Status);
    }

    [Fact]
    public void SaveTranscriptionError_SetsErrorMessageAndStatus()
    {
        var record = new CommandRecord();

        record.SaveTranscriptionError("STT failed");

        Assert.Equal("STT failed", record.ErrorMessage);
        Assert.Equal(CommandStatus.SttFailed, record.Status);
    }

    [Fact]
    public void SaveLlmResult_SetsResponseTextAndConversationAndDurationAndStatus()
    {
        var record = new CommandRecord();
        record.SaveTranscriptionResult("set a timer", 100.0);

        record.SaveLlmResult("Sure, 5 minute timer.", "[{\"role\":\"user\"}]", 2500.0);

        Assert.Equal("Sure, 5 minute timer.", record.ResponseText);
        Assert.Equal("[{\"role\":\"user\"}]", record.LlmConversationJson);
        Assert.Equal(2500.0, record.LlmDurationMs);
        Assert.Equal(CommandStatus.LlmSuccess, record.Status);
    }

    [Fact]
    public void SaveLlmError_SetsErrorMessageAndStatus()
    {
        var record = new CommandRecord();
        record.SaveTranscriptionResult("set a timer", 100.0);

        record.SaveLlmError("LLM timeout");

        Assert.Equal("LLM timeout", record.ErrorMessage);
        Assert.Equal(CommandStatus.LlmFailed, record.Status);
    }

    [Fact]
    public void SaveTtsResult_SetsOutputAudioAndFormatAndDurationAndStatus()
    {
        var record = new CommandRecord();
        record.SaveTranscriptionResult("set a timer", 100.0);
        record.SaveLlmResult("Timer set.", "[]", 2000.0);
        var audioBytes = new byte[] { 1, 2, 3 };

        record.SaveTtsResult(audioBytes, "opus", 800.0);

        Assert.Equal(audioBytes, record.OutputAudio);
        Assert.Equal("opus", record.OutputAudioFormat);
        Assert.Equal(800.0, record.TtsDurationMs);
        Assert.Equal(CommandStatus.TtsSuccess, record.Status);
    }

    [Fact]
    public void SaveTtsError_SetsErrorMessageAndStatus()
    {
        var record = new CommandRecord();
        record.SaveTranscriptionResult("set a timer", 100.0);
        record.SaveLlmResult("Timer set.", "[]", 2000.0);

        record.SaveTtsError("TTS engine crashed");

        Assert.Equal("TTS engine crashed", record.ErrorMessage);
        Assert.Equal(CommandStatus.TtsFailed, record.Status);
    }

    [Fact]
    public void SaveLlmResult_WhenSttHasNotSucceeded_Throws()
    {
        var record = new CommandRecord();

        Assert.Throws<CommandTransitionException>(() =>
            record.SaveLlmResult("response", "[]", 1000));
    }

    [Fact]
    public void Complete_SetsCompletedStatusAndTotalDuration()
    {
        var record = new CommandRecord();
        record.SaveTranscriptionResult("hello", 100);
        record.SaveLlmResult("response", "[]", 2000);
        record.SaveTtsResult(new byte[] { 1, 2, 3 }, "opus", 500);

        record.Complete(2600);

        Assert.Equal(CommandStatus.Completed, record.Status);
        Assert.Equal(2600, record.TotalDurationMs);
    }

    [Fact]
    public void Complete_WhenTtsHasNotSucceeded_Throws()
    {
        var record = new CommandRecord();
        record.SaveTranscriptionResult("hello", 100);
        record.SaveLlmResult("response", "[]", 2000);

        Assert.Throws<CommandTransitionException>(() => record.Complete(2600));
    }

    [Fact]
    public void Fail_FromInProgressState_SetsFailed()
    {
        var record = new CommandRecord();

        record.Fail("boom");

        Assert.Equal(CommandStatus.Failed, record.Status);
        Assert.Equal("boom", record.ErrorMessage);
    }

    [Fact]
    public void Cancel_FromInProgressState_SetsCanceled()
    {
        var record = new CommandRecord();

        record.Cancel("client disconnected");

        Assert.Equal(CommandStatus.Canceled, record.Status);
        Assert.Equal("client disconnected", record.ErrorMessage);
    }

    [Fact]
    public void Fail_WhenAlreadyTerminal_DoesNotChangeStatus()
    {
        var record = new CommandRecord();
        record.SaveTranscriptionError("stt failed");

        record.Fail("later failure");

        Assert.Equal(CommandStatus.SttFailed, record.Status);
        Assert.Equal("stt failed", record.ErrorMessage);
    }

    [Fact]
    public void Cancel_WhenAlreadyTerminal_DoesNotChangeStatus()
    {
        var record = new CommandRecord();
        record.SaveTranscriptionError("stt failed");

        record.Cancel("canceled");

        Assert.Equal(CommandStatus.SttFailed, record.Status);
    }

    [Fact]
    public void DefaultStatus_IsReceived()
    {
        var record = new CommandRecord();

        Assert.Equal(CommandStatus.Received, record.Status);
    }

    [Fact]
    public void FullPipeline_UpdatesStatusThroughAllStages()
    {
        var record = new CommandRecord();

        record.SaveTranscriptionResult("set a timer", 100.0);
        Assert.Equal(CommandStatus.SttSuccess, record.Status);

        record.SaveLlmResult("Timer set.", "[]", 2000.0);
        Assert.Equal(CommandStatus.LlmSuccess, record.Status);

        record.SaveTtsResult([0, 0], "opus", 500.0);
        Assert.Equal(CommandStatus.TtsSuccess, record.Status);

        record.Complete(2600.0);
        Assert.Equal(CommandStatus.Completed, record.Status);
        Assert.Equal(2600.0, record.TotalDurationMs);
    }

    [Fact]
    public void ErrorAtAnyStage_OverridesPreviousStatus()
    {
        var record = new CommandRecord();

        record.SaveTranscriptionResult("hello", 100.0);
        Assert.Equal(CommandStatus.SttSuccess, record.Status);

        record.SaveLlmError("Model unavailable");
        Assert.Equal(CommandStatus.LlmFailed, record.Status);
        Assert.Equal("Model unavailable", record.ErrorMessage);
    }
}
