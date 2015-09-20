using System.Linq;
using System.Web;

namespace Maps.API
{
    internal class SophontCodesHandler : DataHandlerBase
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }

        protected override string ServiceName { get { return "sophontcodes"; } }

        public override void Process(HttpContext context)
        {
            SendResult(context, 
                SecondSurvey.SophontCodes.Select(code => new Results.SophontCode(code, SecondSurvey.SophontCodeToName(code))).ToList());
        }
    }

    internal class AllegianceCodesHandler : DataHandlerBase
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }

        protected override string ServiceName { get { return "allegiancecodes"; } }

        public override void Process(HttpContext context)
        {
            SendResult(context, SecondSurvey.AllegianceCodes.Select(
                code => new Results.AllegianceCode(code, SecondSurvey.GetStockAllegianceFromCode(code).Name)).ToList());
        }
    }
}

namespace Maps.API.Results
{
    public class SophontCode
    {
        public SophontCode() { }
        public SophontCode(string code, string name)
        {
            Code = code;
            Name = name;
        }

        public string Code { get; set; }
        public string Name { get; set; }
    }
    public class AllegianceCode
    {
        public AllegianceCode() { }
        public AllegianceCode(string code, string name)
        {
            Code = code;
            Name = name;
        }

        public string Code { get; set; }
        public string Name { get; set; }
    }
}
