public class AppStateService
{
    public VoiceAssistantState CurrentState { get; set; } = VoiceAssistantState.ListeningForWakeWord;
}
