namespace AquaMai.Config.Types;

public enum IOKeyMap
{
    None = 0,
    Select, // Trigger Select1P or Select2P based on which player the controller is connected as
    Select1P,
    Select2P,
    Service,
    Test,
}
