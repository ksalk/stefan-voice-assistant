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

        record.SaveLlmError("LLM timeout");

        Assert.Equal("LLM timeout", record.ErrorMessage);
        Assert.Equal(CommandStatus.LlmFailed, record.Status);
    }

    [Fact]
    public void SaveTtsResult_SetsOutputAudioAndFormatAndDurationAndStatus()
    {
        var record = new CommandRecord();
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

        record.SaveTtsError("TTS engine crashed");

        Assert.Equal("TTS engine crashed", record.ErrorMessage);
        Assert.Equal(CommandStatus.TtsFailed, record.Status);
    }

    [Fact]
    public void SetTotalDuration_SetsTotalDurationMs()
    {
        var record = new CommandRecord();

        record.SetTotalDuration(3500.0);

        Assert.Equal(3500.0, record.TotalDurationMs);
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

        record.SetTotalDuration(2600.0);
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
