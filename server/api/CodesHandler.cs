using System.Linq;
using System.Web;

namespace Maps.API
{
    internal class SophontCodesHandler : DataHandlerBase
    {
        protected override string ServiceName => "sophontcodes";
        protected override DataResponder GetResponder(HttpContext context)
        {
            return new Responder(context);
        }
        private class Responder : DataResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override string DefaultContentType => System.Net.Mime.MediaTypeNames.Text.Xml;
            public override void Process()
            {
                SendResult(Context,
                    SecondSurvey.SophontCodes.Select(code => new Results.SophontCode(code, SecondSurvey.SophontCodeToName(code))).ToList());
            }
        }
    }

    internal class AllegianceCodesHandler : DataHandlerBase
    {
        protected override string ServiceName => "allegiancecodes";
        protected override DataResponder GetResponder(HttpContext context)
        {
            return new Responder(context);
        }
        private class Responder : DataResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override string DefaultContentType => System.Net.Mime.MediaTypeNames.Text.Xml;

            public override void Process()
            {
                SendResult(Context, SecondSurvey.AllegianceCodes.Select(
                    code => new Results.AllegianceCode(code, SecondSurvey.GetStockAllegianceFromCode(code).Name)).ToList());
            }
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
