namespace SAI_OT_Apps.Server.Models
{
    public class TagTested
    {
        public string Function { get; set; }
        public string Tag { get; set; }
        public string Value { get; set; }
        public string Result { get; set; }
    }

    public class CodeTest
    {
        public string Tag { get; set; } // Tag do CHECK
        public List<TagTested> TagsTested { get; set; }
        public string Function { get; set; }
        public string Value { get; set; } // Valor do CHECK
        public string Result { get; set; } // Resultado do CHECK
    }

}
