(function(global) {
  'use strict';

  function fromEHex(c) {
    return '0123456789ABCDEFGHJKLMNPQRSTUVWXYZ'.indexOf(c.toUpperCase());
  }
  function numberWithCommas(x) {
    return String(x).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
  }

  var STARPORT_TABLE = {
    A: 'Excellent',
    B: 'Good',
    C: 'Routine',
    D: 'Poor',
    E: 'Frontier Installation',
    X: 'None'
  };

  var SIZ_TABLE = {
    1: '1,600km',
    2: '3,200km',
    3: '4,800km',
    4: '6,400km',
    5: '8,000km',
    6: '9,600km',
    7: '11,200km',
    8: '12,800km',
    9: '14,400km',
    A: '16,000km'
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
    E: 'Ellipsoid',
    F: 'Thin, low'
  };

  var HYD_TABLE = {
    0: 'No water',
    1: '10%',
    2: '20%',
    3: '30%',
    4: '40%',
    5: '50%',
    6: '60%',
    7: '70%',
    8: '80%',
    9: '90%',
    A: '100%'
  };

  var POP_TABLE = {
    0: 'Few or none',
    1: 'Tens',
    2: 'Hundreds',
    3: 'Thousands',
    4: 'Tens of thousands',
    5: 'Hundreds of thousands',
    6: 'Millions',
    7: 'Tens of millions',
    8: 'Hundreds of millions',
    9: 'Billions',
    A: 'Tens of billions'
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
    W: 'Committee',
    X: 'Droyne Hierarchy'
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
    S: 'Special/Variable situation'
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
    L: 'Comprehensible only as technological magic'
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
    Px: 'Prison, Exile Camp',
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
    Rs: 'Research Station'
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
    [ /^\[.*\]$/, 'Homeworld'],
    [ /^\(.*\)$/, 'Homeworld'],
    [ /^\(.*\)(\d)$/, 'Homeworld, Population $1$`0%'],
    [ /^\w\w\w\w(\d)$/, 'Sophont, Population $1$`0%'],
    [ /^\w\w\w\wW$/, 'Sophont, Population 100%']
  ];

  var BASE_TABLE = {
    A: ['Naval Base', 'Scout Base'],
    B: ['Naval Base', 'Scout Way Station'],
    C: ['Corsair Base'],
    D: ['Naval Depot'],
    E: ['Embassy Center'],
    F: ['Military Base', 'Naval Base'],
    G: ['Naval Base'],
    H: ['Naval Base', 'Corsair Base'],
    J: ['Naval Base'],
    K: ['Naval Base'],
    L: ['Naval Base'],
    M: ['Military Base'],
    N: ['Naval Base'],
    O: ['Naval Outpost'],
    P: ['Naval Base'],
    Q: ['Military Garrison'],
    R: ['Clan Base'],
    S: ['Scout Base'],
    T: ['Tlauku Base'],
    U: ['Tlauku Base', 'Clan Base'],
    V: ['Scout/Exploration Base'],
    W: ['Scout Way Station'],
    X: ['Relay Station'],
    Y: ['Depot'],
    Z: ['Naval/Military Base']
  };

  function renderWorld(data) {
    var world = data.Worlds[0];
    if (!world) return;

    var uwp = world.UWP;
    world.UWP = {
      Starport: uwp.substring(0, 1),
      Siz: uwp.substring(1, 2),
      Atm: uwp.substring(2, 3),
      Hyd: uwp.substring(3, 4),
      Pop: uwp.substring(4, 5),
      Gov: uwp.substring(5, 6),
      Law: uwp.substring(6, 7),
      Tech: uwp.substring(8, 9)
    };

    world.UWP.StarportBlurb = STARPORT_TABLE[world.UWP.Starport];
    world.UWP.SizBlurb = SIZ_TABLE[world.UWP.Siz];
    world.UWP.AtmBlurb = ATM_TABLE[world.UWP.Atm];
    world.UWP.HydBlurb = HYD_TABLE[world.UWP.Hyd];
    world.UWP.PopBlurb = POP_TABLE[world.UWP.Pop];
    world.UWP.GovBlurb = GOV_TABLE[world.UWP.Gov];
    world.UWP.LawBlurb = LAW_TABLE[world.UWP.Law];
    world.UWP.TechBlurb = TECH_TABLE[world.UWP.Tech];

    var pbg = world.PBG;
    world.PBG = {
      Pop: pbg.substring(0, 1),
      Belts: fromEHex(pbg.substring(1, 2)),
      GG: fromEHex(pbg.substring(2, 3))
    };
    world.PopMult = world.PBG.Pop;
    world.PopExp  = fromEHex(world.UWP.Pop);
    world.TotalPopulation = numberWithCommas(world.PopMult * Math.pow(10, world.PopExp));

    if (world.Ix) {
      var ix = (world.Ix || '').replace(/^{\s*|\s*}$/g, '');
      world.Ix = {
        Imp: ix
      };
    }

    if (world.Ex) {
      var ex = world.Ex.replace(/^\(\s*|\s*\)$/g, '');
      world.Ex = {
        Res: ex.substring(0, 1),
        Lab: ex.substring(1, 2),
        Inf: ex.substring(2, 3),
        EffSign: ex.substring(3, 4),
        EffValue: ex.substring(4, 5)
      };
    }

    if (world.Cx) {
      var cx = world.Cx.replace(/^\[\s*|\s*\]$/g, '');
      world.Cx = {
        Hom: cx.substring(0, 1),
        Acc: cx.substring(1, 2),
        Str: cx.substring(2, 3),
        Sym: cx.substring(3, 4)
      };
    }

    if (world.Nobility) {
      world.Nobility = world.Nobility.split('').map(function(s){
        return s.replace(/./, function(n) { return NOBILITY_TABLE[n] || '???'; });
      });
    }

    if (world.Remarks) {
      world.Remarks = world.Remarks.match(/\([^)]*\)\d*|\[[^\]]*\]\d*|\S+/g).map(function(s){
        if (s in REMARKS_TABLE) return {code: s, detail: REMARKS_TABLE[s]};
        for (var i = 0; i < REMARKS_PATTERNS.length; ++i) {
          var pattern = REMARKS_PATTERNS[i][0], replacement = REMARKS_PATTERNS[i][1];
          if (pattern.test(s)) return {code: s, detail: s.replace(pattern, replacement)};
        }
        return {code: s, detail: '???'};
      });
    }

    world.Bases = (function(code) {
      if (code in BASE_TABLE) return BASE_TABLE[code];
      return [];
    }(world.Bases));

    world.Stars = world.Stellar.split(/\s+(?!Ia|Ib|II|III|IV|V|VI|VII|D)/);

    world.Zone = (function(zone) {
      switch (zone) {
      case 'A': return { rule: 'Caution', rating: 'Amber'};
      case 'R': return { rule: 'Restricted', rating: 'Red'};
      case 'B': return { rule: 'Technologically Elevated Dictatorship', rating: 'c/o Coalition Data Services'};
      case 'F': return { rule: 'Forbidden', rating: 'c/o Consulate Data Services'};
      case 'U': return { rule: 'Unabsorbed', rating: 'c/o Consulate Data Services'};
      default: return { rule: 'No Restrictions', rating: 'Green'};
      }
    }(world.Zone));

    var $ = function(s) { return document.querySelector(s); };
    var template = Handlebars.compile($('#world-template').innerHTML);
    $('#world-data').innerHTML = template(world);

    $('#world-image').classList.add('Hyd' + world.UWP.Hyd);
    $('#world-image').classList.add('Siz' + world.UWP.Siz);
    $('#world-image .disc').src = 'res/Candy/' + (world.UWP.Siz === '0' ? 'Belt' : 'Hyd' + world.UWP.Hyd) + '.png';
    $('#world-image').style.display = 'block';
  }

  function renderNeighborhood(data) {
    var $ = function(s) { return document.querySelector(s); };
    var template = Handlebars.compile($('#neighborhood-template').innerHTML);
    $('#neighborhood-data').innerHTML = template(data);
  }

  window.addEventListener('DOMContentLoaded', function() {
    var $ = function(s) { return document.querySelector(s); };

    function fetch(url, callback, errback) {
      var xhr = new XMLHttpRequest();
      var async = true;
      xhr.open('GET', url, true);
      xhr.onreadystatechange = function() {
        if (xhr.readyState !== XMLHttpRequest.DONE) return;
        if (xhr.status === 200)
          callback(xhr.responseText);
        else
          errback(xhr.responseText);
      };
      xhr.send();
    }

    var query = (function(s) {
      var q = {};
      if (s) s.substring(1).split('&').forEach(function(pair) {
        pair = pair.split('=');
        q[decodeURIComponent(pair[0])] = decodeURIComponent(pair[1]);
      });
      return q;
    }(document.location.search));

    var sector = query['sector'] || 'spin';
    var hex = query['hex'] || '1910';

    fetch('//travellermap.com/data/'+sector+'/'+hex+'?accept=application/json', function(data) {
      renderWorld(JSON.parse(data));
    });
    fetch('//travellermap.com/data/'+sector+'/'+hex+'/jump/2?accept=application/json', function(data) {
      renderNeighborhood(JSON.parse(data));
    });
    var mapurl = '//travellermap.com/data/'+sector+'/'+hex+'/jump/2/image?scale=48&border=0';
    if (window.devicePixelRatio > 1) mapurl += '&dpr=' + window.devicePixelRatio;
    $('#jumpmap').src = mapurl;
  });

}(this));
