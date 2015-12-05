(function(global) {
  'use strict';

  var $ = function(s) { return document.querySelector(s); };

  function numberWithCommas(x) {
    return String(x).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
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
    Y: 'None'
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
    X: 'Unknown'
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
    X: 'Unknown'
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
    X: 'Unknown'
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
    X: 'Unknown'
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
    X: 'Unknown'
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
    X: 'Unknown'
  };

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
    H: 'Emperor'
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
    Xb: 'Xboat Station'
  };

  var REMARKS_PATTERNS = [
    // Special
    [ /^Rs\w$/, 'Research Station'],

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
    [ /^([A-Z][A-Za-z']{3})([0-9W])$/, decodeSophontPopulation],
    [ /^([ACDFHIMVXZ])([0-9w])$/, decodeSophontPopulation],

    // Comments
    [ /^\{.*\}$/, '']
  ];

  var BASE_TABLE = {
    C: 'Corsair Base',
    D: 'Naval Depot',
    E: 'Embassy',
    K: 'Naval Base',
    L: 'Naval Base', // Obsolete
    M: 'Military Base',
    N: 'Naval Base',
    O: 'Naval Outpost', // Obsolete
    R: 'Clan Base',
    S: 'Scout Base',
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

  function decodeSophontPopulation(match, code, pop) {
    var name = SOPHONT_TABLE[code] || 'Sophont';
    if (pop === '0')
      pop = '< 10%';
    else if (pop === 'W' || pop === 'w')
      pop = '100%';
    else
      pop = pop + '0%';
    return name + ', Population ' + pop;
  }

  function splitUWP(uwp) {
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
  }
  function splitPBG(pbg) {
    if (pbg === 'XXX')
      return { Pop: -1, Belts: '???', GG: '???' };
    return {
      Pop: Traveller.fromHex(pbg.substring(0, 1)),
      Belts: Traveller.fromHex(pbg.substring(1, 2)),
      GG: Traveller.fromHex(pbg.substring(2, 3))
    };
  }

  function renderWorld(data, sophonts) {
    var world = data.Worlds[0];
    if (!world) return;

    var isPlaceholder = world.UWP === 'XXXXXXX-X';

    world.UWP = splitUWP(world.UWP);
    world.UWP.StarportBlurb = STARPORT_TABLE[world.UWP.Starport];
    world.UWP.SizBlurb = SIZ_TABLE[world.UWP.Siz];
    world.UWP.AtmBlurb = ATM_TABLE[world.UWP.Atm];
    world.UWP.HydBlurb = HYD_TABLE[world.UWP.Hyd];
    world.UWP.PopBlurb = POP_TABLE[world.UWP.Pop];
    world.UWP.GovBlurb = isPlaceholder ? 'Unknown' : GOV_TABLE[world.UWP.Gov];
    world.UWP.LawBlurb = LAW_TABLE[world.UWP.Law];
    world.UWP.TechBlurb = TECH_TABLE[world.UWP.Tech];

    world.PBG = splitPBG(world.PBG);
    world.PopMult = world.PBG.Pop;
    world.PopExp  = Traveller.fromHex(world.UWP.Pop);
    if (world.PopExp > 0 && world.PopMult === 0)
      world.PopMult = 1;
    if (world.PopExp >= 0 && world.PopMult >= 0)
      world.TotalPopulation = numberWithCommas(world.PopMult * Math.pow(10, world.PopExp));

    var UNICODE_MINUS = '\u2212'; // U+2212 MINUS SIGN

    if (world.Ix) {
      var ix = (world.Ix || '').replace(/^{\s*|\s*}$/g, '');
      ix = ix.replace('-', UNICODE_MINUS);
      world.Ix = {
        Imp: ix
      };
    }

    if (world.Ex) {
      var ex = world.Ex.replace(/^\(\s*|\s*\)$/g, '');
      ex = ex.replace('-', UNICODE_MINUS);
      world.Ex = {
        Res: ex.substring(0, 1),
        Lab: ex.substring(1, 2),
        Inf: ex.substring(2, 3),
        Eff: ex.substring(3)
      };
      ['Res', 'Lab', 'Inf'].forEach(function(s) {
        world.Ex[s + 'Blurb'] = Traveller.fromHex(world.Ex[s]);
      });
      world.Ex.EffBlurb = world.Ex.Eff;
    }

    if (world.Cx) {
      var cx = world.Cx.replace(/^\[\s*|\s*\]$/g, '');
      world.Cx = {
        Hom: cx.substring(0, 1),
        Acc: cx.substring(1, 2),
        Str: cx.substring(2, 3),
        Sym: cx.substring(3, 4)
      };
      ['Hom', 'Acc', 'Str', 'Sym'].forEach(function(s) {
        world.Cx[s + 'Blurb'] = Traveller.fromHex(world.Cx[s]);
      });
    }

    if (world.Nobility) {
      world.Nobility = world.Nobility.split('').map(function(s){
        return s.replace(/./, function(n) { return NOBILITY_TABLE[n] || '???'; });
      });
    }

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

    world.Bases = (function(code) {
      return (code || '').split('').map(function(code) { return BASE_TABLE[code]; });
    }(world.Bases));

    world.Stars = world.Stellar.split(/\s+(?!Ia|Ib|II|III|IV|V|VI|VII|D)/);

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

    if (world.Worlds) {
      world.Worlds = Number(world.Worlds);
      world.OtherWorlds = world.Worlds - 1 - world.PBG.Belts - world.PBG.GG;
    }

    function hasCode(c) {
      return world.Remarks.some(function(r) { return r.code === c; });
    }

    var template = Handlebars.compile($('#world-template').innerHTML);
    $('#world-data').innerHTML = template(world);

    document.title = Handlebars.compile(
      '{{{Name}}} ({{{Sector}}} {{{Hex}}}) - World Data Sheet')(world);

    if ('history' in window && 'replaceState' in window.history) {
      var url = window.location.href.replace(/\?.*$/, '') + '?sector=' + world.Sector + '&hex=' + world.Hex;
      window.history.replaceState(null, document.title, url);
    }

    if (isPlaceholder) {
      $('#world-image').classList.add('unknown');
    } else {
      $('#world-image').classList.add('Hyd' + world.UWP.Hyd);
      $('#world-image').classList.add('Siz' + world.UWP.Siz);
      $('#world-image .disc').src = 'res/Candy/' +
        (world.UWP.Siz === '0' ? 'Belt' : 'Hyd' + world.UWP.Hyd) + '.png';
    }
    $('#world-image').style.display = 'block';

    if (hasCode('Sa'))
      $('#world-image .background').src = 'res/world/gg.jpg';

    // Try loading pre-rendered; if it works, use it instead.
    if (!isPlaceholder) {
      var img = document.createElement('img');
      img.src = 'res/Candy/worlds/' + encodeURIComponent(world.Sector + ' ' + world.Hex) + '.png';
      img.onload = function() {
        $('#world-image .disc').src = img.src;
      };
    }
  }

  function renderNeighborhood(data) {

    // Make hi-pop worlds uppercase
    data.Worlds.forEach(function(world) {
      var pop = Traveller.fromHex(splitUWP(world.UWP).Pop);
      if (pop >= 9)
        world.Name = world.Name.toUpperCase();
    });

    var template = Handlebars.compile($('#neighborhood-template').innerHTML);
    $('#neighborhood-data').innerHTML = template(data);
  }

  window.addEventListener('DOMContentLoaded', function() {
    var query = Util.parseURLQuery(document.location);

    if ('nopage' in query)
      document.body.classList.add('nopage');

    var coords;
    if ('sector' in query && 'hex' in query)
      coords = {sector: query.sector, hex: query.hex};
    else if ('x' in query && 'y' in query)
      coords = {x: query.x, y: query.y};
    else
      coords = {sector: 'spin', hex: '1910'};

    Promise.all([
      fetch(Traveller.MapService.makeURL('/api/coordinates?', coords))
        .then(function(response) {
          if (!response.ok) throw Error(response.statusText);
          return response.json();
        }),
      fetch(Traveller.MapService.makeURL('/t5ss/sophonts'))
        .then(function(response) {
          if (!response.ok) throw Error(response.statusText);
          return response.json();
        })
        .then(function(sophonts) {
          sophonts.forEach(function(sophont) {
            SOPHONT_TABLE[sophont.Code] = sophont.Name;
          });
        })
    ])
      .then(function(results) { return results[0]; })
      .then(function(coords) {
      var JUMP = 2;
      var SCALE = 48;

      var promises = [];
      promises.push(
        fetch(Traveller.MapService.makeURL('/api/jumpworlds?',
                                           {x: coords.x, y: coords.y, jump: 0}))
          .then(function(response) {
            if (!response.ok) throw Error(response.statusText);
            return response.json();
          })
          .then(function(json) {
            renderWorld(json);
          }));

      if (!('nopage' in query)) promises.push(
        fetch(Traveller.MapService.makeURL('/api/jumpworlds?',
                                           {x: coords.x, y: coords.y, jump: JUMP}))
          .then(function(response) {
            return response.json();
          })
          .then(function(json) {
            if (!('nohood' in query))
              renderNeighborhood(json);
          })
          .then(function() {
            var mapParams = {
              x: coords.x,
              y: coords.y,
              jump: JUMP,
              scale: SCALE,
              border: 0};
            if (window.devicePixelRatio > 1)
              mapParams.dpr = window.devicePixelRatio;
            $('#jumpmap').src = Traveller.MapService.makeURL('/api/jumpmap?', mapParams);

            $('#jumpmap').addEventListener('click', function(event) {
              var result = jmapToCoords(event, JUMP, SCALE, coords.x, coords.y);
              if (result)
                window.location.search = '?x=' + result.x + '&y=' + result.y;
            });
          })
      );

      return Promise.all(promises);
    }, function(reason) {
      console.error(reason);
    });
  });

  function jmapToCoords(event, jump, scale, x, y) {
    // TODO: Reject hexes greater than J distance?

    var rect = event.target.getBoundingClientRect();
    var w = rect.right - rect.left;
    var h = rect.bottom - rect.top;

    var scaleX = Math.cos(Math.PI / 6) * scale, scaleY = scale;
    var dx = ((event.clientX  - rect.left - w / 2) / scaleX);
    var dy = ((event.clientY - rect.top - h / 2) / scaleY);

    function p(n) { return Math.abs(Math.round(n) - n); }
    var THRESHOLD = 0.4;

    if (p(dx) > THRESHOLD) return null;
    dx = Math.round(dx);
    if (x % 2)
      dy += (dx % 2) ? 0.5 : 0;
    else
      dy -= (dx % 2) ? 0.5 : 0;
    if (p(dy) > THRESHOLD) return null;
    dy = Math.round(dy);

    return { x: x + dx, y: y + dy };
  }

}(this));
