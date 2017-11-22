/*global Traveller, Util, Handlebars */ // for lint and IDEs
(function(global) {
  'use strict';

  var $ = function(s) { return document.querySelector(s); };
  var $$ = function(s) { return document.querySelectorAll(s); };

  function worldImageURL(world, type) {
    var S3_PREFIX = 'https://s3.amazonaws.com/travellermap/images/';
    switch (type) {
    case 'map':
      return S3_PREFIX + 'maps/'
        + encodeURIComponent(world.SectorAbbreviation) + ' '
        + encodeURIComponent(world.Hex) + '.png';
    case 'map_thumb':
      return S3_PREFIX + 'maps/thumbs/'
        + encodeURIComponent(world.SectorAbbreviation) + ' '
        + encodeURIComponent(world.Hex) + '.jpg';
    case 'render':
      return S3_PREFIX + 'worlds/'
        + encodeURIComponent(world.SectorAbbreviation) + ' '
        + encodeURIComponent(world.Hex) + '.png';
    case 'generic':
      return S3_PREFIX + 'generic_worlds/'
        + (world.UWP.Siz === '0' ? 'Belt' : 'Hyd' + world.UWP.Hyd) + '.png';
    case 'background':
      return S3_PREFIX + 'world_backgrounds/' + world;
    }
  }

  var STARPORT_TABLE = {
    // Starports
    A: 'Excellent',
    B: 'Good',
    C: 'Routine',
    D: 'Poor',
    E: 'Frontier Installation',
    X: 'None or Unknown',
    // Spaceports
    F: 'Good',
    G: 'Poor',
    H: 'Primitive',
    Y: 'None',
    '?': 'Unknown'
  };

  var SIZ_TABLE = {
    0: 'Asteroid Belt',
    S: 'Small World', // MegaTraveller
    1: '1,600km',
    2: '3,200km',
    3: '4,800km',
    4: '6,400km',
    5: '8,000km',
    6: '9,600km',
    7: '11,200km',
    8: '12,800km',
    9: '14,400km',
    A: '16,000km',
    B: '17,600km',
    C: '19,200km',
    D: '20,800km',
    E: '22,400km',
    F: '24,000km',
    X: 'Unknown',
    '?': 'Unknown'
  };

  var ATM_TABLE = {
    0: 'No atmosphere',
    1: 'Trace',
    2: 'Very thin; Tainted',
    3: 'Very thin',
    4: 'Thin; Tainted',
    5: 'Thin',
    6: 'Standard',
    7: 'Standard; Tainted',
    8: 'Dense',
    9: 'Dense; Tainted',
    A: 'Exotic',
    B: 'Corrosive',
    C: 'Insidious',
    D: 'Dense, high',
    E: 'Thin, low',
    F: 'Unusual',
    X: 'Unknown',
    '?': 'Unknown'
  };

  var HYD_TABLE = {
    0: 'Desert World',
    1: '10%',
    2: '20%',
    3: '30%',
    4: '40%',
    5: '50%',
    6: '60%',
    7: '70%',
    8: '80%',
    9: '90%',
    A: 'Water World',
    X: 'Unknown',
    '?': 'Unknown'
  };

  var POP_TABLE = {
    0: 'Unpopulated',
    1: 'Tens',
    2: 'Hundreds',
    3: 'Thousands',
    4: 'Tens of thousands',
    5: 'Hundreds of thousands',
    6: 'Millions',
    7: 'Tens of millions',
    8: 'Hundreds of millions',
    9: 'Billions',
    A: 'Tens of billions',
    B: 'Hundreds of billions',
    C: 'Trillions',
    D: 'Tens of trillions',
    E: 'Hundreds of tillions',
    F: 'Quadrillions',
    X: 'Unknown',
    '?': 'Unknown'
  };

  var GOV_TABLE = {
    0: 'No Government Structure',
    1: 'Company/Corporation',
    2: 'Participating Democracy',
    3: 'Self-Perpetuating Oligarchy',
    4: 'Representative Democracy',
    5: 'Feudal Technocracy',
    6: 'Captive Government / Colony',
    7: 'Balkanization',
    8: 'Civil Service Bureaucracy',
    9: 'Impersonal Bureaucracy',
    A: 'Charismatic Dictator',
    B: 'Non-Charismatic Dictator',
    C: 'Charismatic Oligarchy',
    D: 'Religious Dictatorship',
    E: 'Religious Autocracy',
    F: 'Totalitarian Oligarchy',
    X: 'Unknown',
    '?': 'Unknown',

    // Legacy/Non-Human
    G: 'Small Station or Facility',
    H: 'Split Clan Control',
    J: 'Single On-world Clan Control',
    K: 'Single Multi-world Clan Control',
    L: 'Major Clan Control',
    M: 'Vassal Clan Control',
    N: 'Major Vassal Clan Control',
    P: 'Small Station or Facility',
    Q: 'Krurruna or Krumanak Rule for Off-world Steppelord',
    R: 'Steppelord On-world Rule',
    S: 'Sept',
    T: 'Unsupervised Anarchy',
    U: 'Supervised Anarchy',
    W: 'Committee'
    //X: 'Droyne Hierarchy' // Need a hack for this

  };

  var LAW_TABLE = {
    0: 'No prohibitions',
    1: 'Body pistols, explosives, and poison gas prohibited',
    2: 'Portable energy weapons prohibited',
    3: 'Machine guns, automatic rifles prohibited',
    4: 'Light assault weapons prohibited',
    5: 'Personal concealable weapons prohibited',
    6: 'All firearms except shotguns prohibited',
    7: 'Shotguns prohibited',
    8: 'Long bladed weapons controlled; open possession prohibited',
    9: 'Possession of weapons outside the home prohibited',
    A: 'Weapon possession prohibited',
    B: 'Rigid control of civilian movement',
    C: 'Unrestricted invasion of privacy',
    D: 'Paramilitary law enforcement',
    E: 'Full-fledged police state',
    F: 'All facets of daily life regularly legislated and controlled',
    G: 'Severe punishment for petty infractions',
    H: 'Legalized oppressive practices',
    J: 'Routinely oppressive and restrictive',
    K: 'Excessively oppressive and restrictive',
    L: 'Totally oppressive and restrictive',
    S: 'Special/Variable situation',
    X: 'Unknown',
    '?': 'Unknown'
  };

  var TECH_TABLE = {
    0: 'Stone Age',
    1: 'Bronze, Iron',
    2: 'Printing Press',
    3: 'Basic Science',
    4: 'External Combustion',
    5: 'Mass Production',
    6: 'Nuclear Power',
    7: 'Miniaturized Electronics',
    8: 'Quality Computers',
    9: 'Anti-Gravity',
    A: 'Interstellar community',
    B: 'Lower Average Imperial',
    C: 'Average Imperial',
    D: 'Above Average Imperial',
    E: 'Above Average Imperial',
    F: 'Technical Imperial Maximum',
    G: 'Robots',
    H: 'Artificial Intelligence',
    J: 'Personal Disintegrators',
    K: 'Plastic Metals',
    L: 'Comprehensible only as technological magic',
    X: 'Unknown',
    '?': 'Unknown'
  };

  var IX_IMP_TABLE = {
    '-3': 'Very unimportant',
    '-2': 'Very unimportant',
    '-1': 'Unimportant',
    '0': 'Unimportant',
    '1': 'Ordinary',
    '2': 'Ordinary',
    '3': 'Ordinary',
    '4': 'Important',
    '5': 'Very important',
    '?': 'Unknown'
  };

  var EX_RESOURCES_TABLE = {
    2: 'Very scarce',
    3: 'Very scarce',
    4: 'Scarce',
    5: 'Scarce',
    6: 'Few',
    7: 'Few',
    8: 'Moderate',
    9: 'Moderate',
    A: 'Abundant',
    B: 'Abundant',
    C: 'Very abundant',
    D: 'Very abundant',
    E: 'Extremely abundant',
    F: 'Extremely abundant',
    G: 'Extremely abundant',
    H: 'Extremely abundant',
    J: 'Extremely abundant',
    '?': 'Unknown'
  };

  var EX_LABOR_TABLE = POP_TABLE;

  var EX_INFRASTRUCTURE_TABLE = {
    0: 'Non-existent',
    1: 'Extremely limited',
    2: 'Extremely limited',
    3: 'Very limited',
    4: 'Very limited',
    5: 'Limited',
    6: 'Limited',
    7: 'Generally available',
    8: 'Generally available',
    9: 'Extensive',
    A: 'Extensive',
    B: 'Very extensive',
    C: 'Very extensive',
    D: 'Comprehensive',
    E: 'Comprehensive',
    F: 'Very comprehensive',
    G: 'Very comprehensive',
    H: 'Very comprehensive',
    '?': 'Unknown'
  };

  var EX_EFFICIENCY_TABLE = {
    '-5': 'Extremely poor',
    '-4': 'Very poor',
    '-3': 'Poor',
    '-2': 'Fair',
    '-1': 'Average',
    '0': 'Average',
    '+1': 'Average',
    '+2': 'Good',
    '+3': 'Improved',
    '+4': 'Advanced',
    '+5': 'Very advanced',
    '?': 'Unknown'
  };

  var CX_HOMOGENEITY_TABLE = {
    0: 'N/A',
    1: 'Monolithic',
    2: 'Monolithic',
    3: 'Monolithic',
    4: 'Harmonious',
    5: 'Harmonious',
    6: 'Harmonious',
    7: 'Discordant',
    8: 'Discordant',
    9: 'Discordant',
    A: 'Discordant',
    B: 'Discordant',
    C: 'Fragmented',
    D: 'Fragmented',
    E: 'Fragmented',
    F: 'Fragmented',
    G: 'Fragmented',
    '?': 'Unknown'
  };

  var CX_ACCEPTANCE_TABLE = {
    0: 'N/A',
    1: 'Extremely xenophobic',
    2: 'Very xenophobic',
    3: 'Xenophobic',
    4: 'Extremely aloof',
    5: 'Very aloof',
    6: 'Aloof',
    7: 'Aloof',
    8: 'Friendly',
    9: 'Friendly',
    A: 'Very friendly',
    B: 'Extremely friendly',
    C: 'Xenophilic',
    D: 'Very Xenophilic',
    E: 'Extremely xenophilic',
    F: 'Extremely xenophilic',
    '?': 'Unknown'
  };

  // TODO: Cx: Strangeness
  // TODO: Cx: Symbols

  var NOBILITY_TABLE = {
    B: 'Knight',
    c: 'Baronet',
    C: 'Baron',
    D: 'Marquis',
    e: 'Viscount',
    E: 'Count',
    f: 'Duke',
    F: 'Subsector Duke',
    G: 'Archduke',
    H: 'Emperor',
    '?': 'Unknown'
  };

  var REMARKS_TABLE = {
    // Planetary
    As: 'Asteroid Belt',
    De: 'Desert',
    Fl: 'Fluid Hydrographics (in place of water)',
    Ga: 'Garden World',
    He: 'Hellworld',
    Ic: 'Ice Capped',
    Oc: 'Ocean World',
    Va: 'Vacuum World',
    Wa: 'Water World',

    // Population
    Di: 'Dieback',
    Ba: 'Barren',
    Lo: 'Low Population',
    Ni: 'Non-Industrial',
    Ph: 'Pre-High Population',
    Hi: 'High Population',

    // Economic
    Pa: 'Pre-Agricultural',
    Ag: 'Agricultural',
    Na: 'Non-Agricultural',
    Px: 'Prison, Exile Camp',
    Pi: 'Pre-Industrial',
    In: 'Industrialized',
    Po: 'Poor',
    Pr: 'Pre-Rich',
    Ri: 'Rich',

    // Climate
    Fr: 'Frozen',
    Ho: 'Hot',
    Co: 'Cold',
    Lk: 'Locked',
    Tr: 'Tropic',
    Tu: 'Tundra',
    Tz: 'Twilight Zone',

    // Secondary
    Fa: 'Farming',
    Mi: 'Mining',
    Mr: 'Military Rule',
    Pe: 'Penal Colony',
    Re: 'Reserve',

    // Political
    Cp: 'Subsector Capital',
    Cs: 'Sector Capital',
    Cx: 'Capital',
    Cy: 'Colony',

    // Special
    Sa: 'Satellite',
    Fo: 'Forbidden',
    Pz: 'Puzzle',
    Da: 'Danger',
    Ab: 'Data Repository',
    An: 'Ancient Site',

    Rs: 'Research Station',
    RsA: 'Research Station Alpha',
    RsB: 'Research Station Beta',
    RsG: 'Research Station Gamma',
    RsD: 'Research Station Delta',
    RsE: 'Research Station Epsilon',
    RsZ: 'Research Station Zeta',
    RsH: 'Research Station Eta',
    RsT: 'Research Station Theta',

    // Legacy
    Nh: 'Non-Hiver Population',
    Nk: "Non-K'kree Population",
    Tp: 'Terra-prime',
    Tn: 'Terra-norm',
    Lt: 'Low Technology',
    Ht: 'High Technology',
    //Fa: 'Fascinating', // Conflicts with T5: Farming.
    St: 'Steppeworld',
    Ex: 'Exile Camp',
    //Pr: 'Prison World', // Conflicts with T5: Pre-Rich.
    Xb: 'Xboat Station',
    Cr: 'Reserve Capital'
  };

  var REMARKS_PATTERNS = [
    // Special
    [/^Rs\w$/, 'Research Station'],
    [/^Rw:?\w$/, 'Refugee World'],

    // Ownership
    [ /^O:\d\d\d\d$/, 'Controlled'],
    [ /^O:\d\d\d\d-\w+$/, 'Controlled'],
    [ /^O:\w\w$/, 'Controlled'],
    [ /^Mr:\d\d\d\d$/, 'Military rule'],

    // Sophonts
    [ /^\[.*\]\??$/, 'Homeworld'],
    [ /^\(.*\)\??$/, 'Homeworld'],
    [ /^\(.*\)(\d)$/, 'Homeworld, Population $1$`0%'],
    [ /^Di\(.*\)$/, 'Homeworld, Extinct'],
    [ /^([A-Z][A-Za-z']{3})([0-9W?])$/, decodeSophontPopulation],
    [ /^([ACDFHIMVXZ])([0-9w])$/, decodeSophontPopulation],

    // Comments
    [ /^\{.*\}$/, '']
  ];

  var BASE_TABLE = {
      C: 'Corsair Base',
      D: 'Naval Depot',
      E: 'Embassy',
      H: 'Hiver Supply Base', // For TNE
      I: 'Interface', // For TNE
      K: 'Naval Base',
      L: 'Naval Base', // Obsolete
      M: 'Military Base',
      N: 'Naval Base',
      O: 'Naval Outpost', // Obsolete
      R: 'Clan Base',
      S: 'Scout Base',
      //    T: 'Terminus',   // For TNE - name Collision
      T: 'Tlauku Base',
      V: 'Exploration Base',
      W: 'Way Station',
      X: 'Relay Station', // Obsolete
      Z: 'Naval/Military Base' // Obsolete
  };

  var SOPHONT_TABLE = {
    // Legacy codes
    'A': 'Aslan',
    'C': 'Chirper',
    'D': 'Droyne',
    'F': 'Non-Hiver',
    'H': 'Hiver',
    'I': 'Ithklur',
    'M': 'Human',
    'V': 'Vargr',
    'X': 'Addaxur',
    'Z': 'Zhodani'
    // T5SS codes populated by live data
  };

  // Promise - resolved once sophont table is fully populated.
  var SOPHONTS_FETCHED = fetch(Traveller.MapService.makeURL('/t5ss/sophonts'))
        .then(function(response) {
          if (!response.ok) throw Error(response.statusText);
          return response.json();
        })
        .then(function(sophonts) {
          sophonts.forEach(function(sophont) {
            SOPHONT_TABLE[sophont.Code] = sophont.Name;
          });
        });

  var fetch_status = new Map();

  function fetchImage(url) {
    if (fetch_status.has(url) && !fetch_status.get(url))
      return Promise.reject(new Error('Image not available'));
    return Util.fetchImage(url)
      .then(function(img) { fetch_status.set(url, true); return img; })
      .catch(function(err) { fetch_status.set(url, false); throw err; });
  }

  function decodeSophontPopulation(match, code, pop) {
    var name = SOPHONT_TABLE[code] || 'Sophont';
    if (pop === '0')
      pop = '< 10%';
    else if (pop === 'W' || pop === 'w')
      pop = '100%';
    else if (pop === '?')
      pop = 'Unknown';
    else
      pop = pop + '0%';
    return name + ', Population ' + pop;
  }

  function numberWithCommas(x) {
    return String(x).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
  }

  Traveller.splitUWP = function splitUWP(uwp) {
    return {
      Starport: uwp.substring(0, 1),
      Siz: uwp.substring(1, 2),
      Atm: uwp.substring(2, 3),
      Hyd: uwp.substring(3, 4),
      Pop: uwp.substring(4, 5),
      Gov: uwp.substring(5, 6),
      Law: uwp.substring(6, 7),
      Tech: uwp.substring(8, 9)
    };
  };

  Traveller.splitPBG = function splitPBG(pbg) {
    function fix(value, replacement) {
      return value === -1 ? replacement : value;
    }
    return {
      Pop: Traveller.fromHex(pbg.substring(0, 1)),
      Belts: fix(Traveller.fromHex(pbg.substring(1, 2)), '???'),
      GG: fix(Traveller.fromHex(pbg.substring(2, 3)), '???')
    };
  };

  function hasCode(world, c) {
    return world.Remarks && world.Remarks.some(function(r) { return r.code === c; });
  }

  Traveller.prepareWorld = function(world) {
    if (!world) return undefined;
    return SOPHONTS_FETCHED.then(function() {
      world.isPlaceholder = (world.UWP === 'XXXXXXX-X' || world.UWP === '???????-?');

      // UWP - StSAHPGL-T
      world.UWP = Traveller.splitUWP(world.UWP);
      world.UWP.StarportBlurb = STARPORT_TABLE[world.UWP.Starport];
      world.UWP.SizBlurb = SIZ_TABLE[world.UWP.Siz];
      world.UWP.AtmBlurb = ATM_TABLE[world.UWP.Atm];
      world.UWP.HydBlurb = HYD_TABLE[world.UWP.Hyd];
      world.UWP.PopBlurb = POP_TABLE[world.UWP.Pop];
      world.UWP.GovBlurb = GOV_TABLE[world.UWP.Gov];
      world.UWP.LawBlurb = LAW_TABLE[world.UWP.Law];
      world.UWP.TechBlurb = TECH_TABLE[world.UWP.Tech];

      // PBG
      world.PBG = Traveller.splitPBG(world.PBG);
      world.PopMult = world.PBG.Pop;
      world.PopExp  = Traveller.fromHex(world.UWP.Pop);
      if (world.PopExp > 0 && world.PopMult === 0)
        world.PopMult = 1;
      if (world.PopExp >= 0 && world.PopMult >= 0)
        world.TotalPopulation = numberWithCommas(world.PopMult * Math.pow(10, world.PopExp));

      var UNICODE_MINUS = '\u2212'; // U+2212 MINUS SIGN

      // Importance {Ix}
      if (!world.Ix) delete world.Ix;
      if (world.Ix) {
        var ix = (world.Ix || '').replace(/^{\s*|\s*}$/g, '');
        world.Ix = {
          Imp: ix
        };
        world.Ix.ImpBlurb = IX_IMP_TABLE[world.Ix.Imp];

        world.Ix.Imp = world.Ix.Imp.replace('-', UNICODE_MINUS);
      }

      // Economics (Ex)
      if (!world.Ex) delete world.Ex;
      if (world.Ex) {
        var ex = world.Ex.replace(/^\(\s*|\s*\)$/g, '');
        world.Ex = {
          Res: ex.substring(0, 1),
          Lab: ex.substring(1, 2),
          Inf: ex.substring(2, 3),
          Eff: ex.substring(3)
        };
        world.Ex.ResBlurb = EX_RESOURCES_TABLE[world.Ex.Res];
        world.Ex.LabBlurb = EX_LABOR_TABLE[world.Ex.Lab];
        world.Ex.InfBlurb = EX_INFRASTRUCTURE_TABLE[world.Ex.Inf];
        world.Ex.EffBlurb = EX_EFFICIENCY_TABLE[world.Ex.Eff];

        world.Ex.Eff = world.Ex.Eff.replace('-', UNICODE_MINUS);
      }

      // Culture [Cx]
      if (!world.Cx) delete world.Cx;
      if (world.Cx) {
        var cx = world.Cx.replace(/^\[\s*|\s*\]$/g, '');
        world.Cx = {
          Hom: cx.substring(0, 1),
          Acc: cx.substring(1, 2),
          Str: cx.substring(2, 3),
          Sym: cx.substring(3, 4)
        };

        world.Cx.HomBlurb = CX_HOMOGENEITY_TABLE[world.Cx.Hom];
        world.Cx.AccBlurb = CX_ACCEPTANCE_TABLE[world.Cx.Acc];
        world.Cx.StrBlurb = Traveller.fromHex(world.Cx.Str);
        world.Cx.SymBlurb = Traveller.fromHex(world.Cx.Sym);
      }

      // Nobility
      if (world.Nobility) {
        world.Nobility = world.Nobility.split('').map(function(s){
          return s.replace(/./, function(n) { return NOBILITY_TABLE[n] || '???'; });
        });
      }

      // Remarks
      if (world.Remarks) {
        world.Remarks = world.Remarks.match(/(Di)?\([^)]*\)[0-9?]?|\[[^\]]*\][0-9?]?|{[^}]*}|\S+/g).map(function(s){
          if (s in REMARKS_TABLE) return {code: s, detail: REMARKS_TABLE[s]};
          for (var i = 0; i < REMARKS_PATTERNS.length; ++i) {
            var pattern = REMARKS_PATTERNS[i][0], replacement = REMARKS_PATTERNS[i][1];
            if (pattern.test(s)) return {code: s, detail: s.replace(pattern, replacement)};
          }
          return {code: s, detail: '???'};
        });
      }

      // Bases
      world.Bases = (function(code) {
        return (code || '').split('').map(function(code) { return BASE_TABLE[code]; });
      }(world.Bases));

      // Stars
      world.Stars = world.Stellar.split(/\s+(?!Ia|Ib|II|III|IV|V|VI|VII|D)/);

      // Zone
      world.Zone = (function(zone) {
        switch (zone) {
        case 'A': return { rule: 'Caution', rating: 'Amber', className: 'amber'};
        case 'R': return { rule: 'Restricted', rating: 'Red', className: 'red'};
        case 'B': return { rule: 'Technologically Elevated Dictatorship',
                           rating: 'c/o Coalition Data Services', className: 'ted'};
        case 'F': return { rule: 'Forbidden', rating: 'c/o Consulate Data Services',
                           className: 'forbidden'};
        case 'U': return { rule: 'Unabsorbed', rating: 'c/o Consulate Data Services',
                           className: 'unabsorbed'};
        default: return { rule: 'No Restrictions', rating: 'Green', className: 'green'};
        }
      }(world.Zone));

      // Worlds
      if (world.Worlds) {
        world.Worlds = Number(world.Worlds);
        world.OtherWorlds = world.Worlds - 1 - world.PBG.Belts - world.PBG.GG;
      }

      // Wiki Links
      function makeWikiURL(suffix) {
        return 'http://wiki.travellerrpg.com/' + encodeURIComponent(suffix.replace(/ /g, '_'));
      }
      world.world_url = makeWikiURL(world.Name + ' (world)');
      world.world_url_noscheme = world.world_url.replace(/^\w+:\/\//, '');
      world.ss_url = makeWikiURL(world.SubsectorName + ' Subsector');
      world.ss_url_noscheme = world.ss_url.replace(/^\w+:\/\//, '');
      world.sector_url = makeWikiURL(world.Sector + ' Sector');
      world.sector_url_noscheme = world.sector_url.replace(/^\w+:\/\//, '');

      return world;
    }).then(function(world) {
      var map_thumb = worldImageURL(world, 'map_thumb');
      if (fetch_status.has(map_thumb)) {
        if (fetch_status.get(map_thumb)) world.map_thumb = map_thumb;
        return world;
      }
      showConsoleNotice();
      return fetchImage(map_thumb)
        .then(function(response) { world.map_thumb = map_thumb; }, function() {})
        .then(function() { return world; });
    });
  };

  function supportsCompositeMode(ctx, mode) {
    var orig = ctx.globalCompositeOperation;
    ctx.globalCompositeOperation = mode;
    var result = ctx.globalCompositeOperation === mode;
    ctx.globalCompositeOperation = orig;
    return result;
  }

  var showConsoleNotice = Util.once(function() {
    if (!console || !console.log) return;
    console.log('The "404 (Not Found)" error for world images is expected, and is not a bug.');
  });

  var renderWorldImageFirstTime = true;
  Traveller.renderWorldImage = function(world, canvas) {
    if (!world) return undefined;

    var w = canvas.width, h = canvas.height;

    var bg = worldImageURL(
      (!world.isPlaceholder && hasCode(world, 'Sa')) ? 'gg.jpg' : 'stars.png',
      'background');

    var SIZES = [
      { width: 0.80, height: 0.45 },
      { width: 0.25, height: 0.25 },
      { width: 0.30, height: 0.30 },
      { width: 0.35, height: 0.35 },
      { width: 0.40, height: 0.40 },
      { width: 0.45, height: 0.45 },
      { width: 0.50, height: 0.50 },
      { width: 0.55, height: 0.55 },
      { width: 0.60, height: 0.60 },
      { width: 0.65, height: 0.65 },
      { width: 0.70, height: 0.70 },
      { width: 0.75, height: 0.75 },
      { width: 0.80, height: 0.80 },
      { width: 0.85, height: 0.85 },
      { width: 0.90, height: 0.90 },
      { width: 0.95, height: 0.95 }
    ];

    var render = worldImageURL(world, 'render');
    var generic = worldImageURL(world, 'generic');
    var isRender = true;

    var size = SIZES[world.UWP.Siz] || {width: 0.5, height: 0.5};

    return Promise.all([
      // Background
      fetchImage(bg),

      // Foreground
      world.isPlaceholder
        ? null
        : fetchImage(render).then(
          function(image) {
            size.height = size.width * image.naturalHeight / image.naturalWidth;
            return image;
          },
          function() {
            showConsoleNotice();
            isRender = false;
            return fetchImage(generic);
          })
    ])
      .then(function(images) {
        var bgimg = images[0];
        var fgimg = images[1]; // null if isPlaceholder

        var ctx = canvas.getContext('2d');
        ctx.save();
        try {
          ctx.imageSmoothingEnabled = true;

          if (!fgimg) {
            ctx.drawImage(bgimg, 0, 0, w, h);
            var label = '?';
            var th = h * 2/3;
            ctx.font = String(th) + 'px sans-serif';
            ctx.fillStyle = 'white';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(label, w/2, h/2);
            return world;
          }

          var iw = w * size.width, ih = h * size.height;
          var ix = (w - iw) / 2, iy = (h - ih) / 2;

          var m;
          if (!isRender &&
              supportsCompositeMode(ctx, 'destination-in') &&
              supportsCompositeMode(ctx, 'destination-over') &&
              supportsCompositeMode(ctx, 'multiply') &&
              world.Stars && (m = /^([OBAFGKM])([0-9])/.exec(world.Stars[0]))) {
            // Advanced - color blend image.
            var t = class2temp(m[1], m[2]);
            var c = temp2color(t);
            ctx.fillStyle = 'rgb(' + c.r + ',' + c.g + ',' + c.b + ')';

            ctx.fillRect(ix, iy, iw, ih);
            ctx.globalCompositeOperation = 'destination-in';
            ctx.drawImage(fgimg, ix, iy, iw, ih);
            ctx.globalCompositeOperation = 'multiply';

            ctx.drawImage(fgimg, ix, iy, iw, ih);

            ctx.globalCompositeOperation = 'destination-over';
            ctx.drawImage(bgimg, 0, 0, w, h);
          } else {
            // Basic - background then foreground.
            ctx.drawImage(bgimg, 0, 0, w, h);
            ctx.drawImage(fgimg, ix, iy, iw, ih);
          }

          return world;
        } finally {
          ctx.restore();
        }
    });
  };

  // Convert stellar class (e.g. 'G', '2') to temperature (Kelvin).
  // Curve fit based on data from:
  // http://www.uni.edu/morgans/astro/course/Notes/section2/spectraltemps.html
  function class2temp(c, f) {
    var n = 'OBAFGKM'.indexOf(c) + Number(f) / 10;
    return 26684.83 * Math.pow(n, -1.127977);
  }

  // Convert temperature (Kelvin) to color {r, g, b} in 0...255.
  // Based on: http://www.zombieprototypes.com/?p=210
  function temp2color(kelvin) {
    function fit(a, b, c, x) { return Math.floor(a + b*x + c * Math.log(x)); }
    var r, g, b;

    if (kelvin < 6600)
      r = 255;
    else
      r = fit(351.97690566805693, 0.114206453784165, -40.25366309332127, (kelvin/100) - 55);

    if (kelvin <= 1000)
      g = 0;
    else if (kelvin < 6600)
      g = fit(-155.25485562709179, -0.44596950469579133, 104.49216199393888, (kelvin/100) - 2);
    else
      g = fit(325.4494125711974, 0.07943456536662342, -28.0852963507957, (kelvin/100) - 50);

    if (kelvin <= 2000)
      b = 0;
    else if (kelvin < 6600)
      b = fit(-254.76935184120902, 0.8274096064007395, 115.67994401066147, (kelvin/100) - 10);
    else
      b = 255;

    return {r:r, g:g, b:b};
  }

}(this));
