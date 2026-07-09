namespace ModelAIIntegrationWebApp.Models
{
    public class ChatTurn
    { 
        public string Question { get; set; } = "";
        public string Answer { get; set; } = "";
        public string ProviderName { get; set; } = "";
        public string ProviderModel { get; set; } = "";
        public List<string> Sources { get; set; } = new();  
    }
}
