using Microsoft.AspNetCore.Mvc;
using TravellerCore.Features;
using TravellerParser;

namespace TravellerAPI.Controllers;
[ApiController]
[Route("[controller]")]
public class ParsingController : ControllerBase
{
    private readonly ILogger<ParsingController> _logger;
    private readonly IParser Parser;

    public ParsingController(ILogger<ParsingController> logger, IParser parser)
    {
        _logger = logger;
        Parser = parser;
    }

    [HttpGet]
    public ActionResult Parse(string format, string sectorData, string sectorMetadata)
    {
        if (Parser.TryParseSector(sectorData, sectorMetadata, out var sector)) return new JsonResult(sector);
        else return new StatusCodeResult(500);
    }
}
