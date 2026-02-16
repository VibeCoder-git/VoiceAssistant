namespace VoiceAssistant.App.Core
{
    public enum AssistantState
    {
        IDLE,
        ACTIVE,
        EXECUTE,
        COOLDOWN // Internal state to handle the cooldown period
    }
}
