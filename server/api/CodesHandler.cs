using Maps.Utilities;
using System.Linq;
using System.Web;

namespace Maps.API
{
    internal class SophontCodesHandler : DataHandlerBase
    {
        protected override DataResponder GetResponder(HttpContext context) => new Responder(context);

        private class Responder : DataResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override string DefaultContentType => ContentTypes.Text.Xml;
            public override void Process(ResourceManager resourceManager)
            {
                SendResult(SecondSurvey.SophontCodes.Select(code =>
                {
                    var sophont = SecondSurvey.SophontForCode(code);
                    return new Results.SophontCode(code, sophont.Name, sophont.Location);
                }).ToList());
            }
        }
    }

    internal class AllegianceCodesHandler : DataHandlerBase
    {
        protected override DataResponder GetResponder(HttpContext context) => new Responder(context);

        private class Responder : DataResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override string DefaultContentType => ContentTypes.Text.Xml;

            public override void Process(ResourceManager resourceManager)
            {
                SendResult(SecondSurvey.AllegianceCodes.Select(
                    code =>
                    {
                        var alleg = SecondSurvey.GetStockAllegianceFromCode(code);
                        return new Results.AllegianceCode(code, alleg.LegacyCode, alleg.Name, alleg.Location);
                    }).ToList());
            }
        }
    }
}

namespace Maps.API.Results
{
    public class SophontCode
    {
        public SophontCode() { }
        public SophontCode(string code, string name, string location)
        {
            Code = code; Name = name; Location = location;
        }

        public string Code { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
    }
    public class AllegianceCode
    {
        public AllegianceCode() { }
        public AllegianceCode(string code, string legacy, string name, string location)
        {
            Code = code;
            LegacyCode = legacy;
            Name = name;
            Location = location;
        }

        public string Code { get; set; }
        public string LegacyCode { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
    }
}
