function pad(s, len) { return s + ' '.repeat(len - s.length); }
function trim(s) { return s.replace(/\s+$/, ''); }

function parse(s) {
  if (/\t/.test(s)) {
    return parseTabDelimited(s);
  }
  if (/^-+( +-+)*$/m.test(s)) {
    return parseColumnDelimited(s);
  }
  return parseSec(s);
}

function parseTabDelimited(s) {
  var lines = s.split('\n').filter(function(s) { return !/^\s*$|^#/.test(s); });
  var fields = lines.shift().split('\t');
  return {
    type: 'tab',
    fields: fields,
    worlds: lines.map(function(line) {
      var cols = line.split('\t');
      var world = {};
      fields.forEach(function(field, index) {
        world[field] = cols[index];
      });
      return world;
    })
  };
}

function parseColumnDelimited(s) {
  var lines = s.split('\n').filter(function(s) { return !/^\s*$|^#/.test(s); });
  var fields = lines.shift();
  var separator = lines.shift(), col = 0, cols = [];
  separator.split(/(\s+)/).forEach(function(s) {
    if (/-+/.test(s))
      cols.push([col, s.length]);
    col += s.length;
  });

  function colsplit(s) {
    return cols.map(function(pair) {
      return trim(s.substr(pair[0], pair[1]));
    });
  }

  fields = colsplit(fields);
  return {
    type: 'col',
    fields: fields,
    worlds: lines.map(function(line) {
      line = colsplit(line);
      var world = {};
      fields.forEach(function(field, index) {
        world[field] = line[index] || '';
      });
      return world;
    })
  };
}

function parseSec(s) {
  s = trim(s);
  var header = [];
  var worlds = [];

  s.split('\n').forEach(function(line) {
    var m;
    if ((m = /^(.*?)\s+(\d\d\d\d)\s+([ABCDEX?][0-9A-Z?]{6}-[0-9A-Z?])\s{1,2}([A-Z1-9* ])\s+(.{10,})\s+([GARBFU])?\s+(\d[0-9A-F][0-9A-F])\s+(\S\S)\s+(.*?)\s*$/.exec(line))) {
      worlds.push({
        Name:        m[1],
        Hex:         m[2],
        UWP:         m[3],
        Base:        trim(m[4] || ''),
        Remarks:     m[5],
        Zone:        trim(m[6] || ''),
        PBG:         m[7],
        Allegiance:  m[8],
        Stars:       m[9]
      });
    } else {
      header.push(trim(line));
    }
  });

  return {
    type: 'sec',
    fields: ['Name', 'Hex', 'UWP', 'Base', 'Remarks', 'Zone', 'PBG', 'Allegiance', 'Stars'],
    header: header,
    worlds: worlds
  };
}

function formatSec(data, options) {
  var out = [].concat(data.header);
  data.worlds.forEach(function(world) {
    out.push(
      pad(world.Name,       20) + ' ' +
      world.Hex                 + ' ' +
      world.UWP                 + ' ' +
      pad(world.Base,        1) + ' ' +
      pad(world.Remarks,    30) + ' ' +
      pad(world.Zone,        1) + ' ' +
      world.PBG                 + ' ' +
      world.Allegiance          + ' ' +
      world.Stars
    );
  });

  return out.join('\n') + '\n';
}


function format(data, options) {
  if (data.type === 'tab')
    return formatTabDelimited(data, options);
  if (data.type === 'col')
    return formatColDelimited(data, options);
  return formatSec(data, options);
}

function formatTabDelimited(data, options) {
  var out = [];
  out.push(data.fields.join('\t'));
  data.worlds.forEach(function(world) {
    out.push(data.fields.map(function(field) {
      return world[field] || '';
    }).join('\t'));
  });
  return out.join('\n') + '\n';
}

function formatColDelimited(data, options) {
  var widths = data.fields.map(function(f) { return f.length; });
  data.worlds.forEach(function(world) {
    data.fields.forEach(function(field, index) {
      var value = world[field];
      widths[index] = Math.max(widths[index], value.length);
    });
  });

  if (options.expand) {
    data.fields.forEach(function(field, index) {
      switch (field) {
        case 'Name':
        case 'Remarks':
        case 'Stellar':
          widths[index] += 15;
          break;

        case '{Ix}':
        case '(Ex)':
        case '[Cx]':
        case 'N':
          widths[index] = Math.max(widths[index] || 0, 7);
          break;

        case 'B':
          widths[index] = Math.max(widths[index] || 0, 2);
          break;

        case 'A':
          widths[index] = Math.max(widths[index] || 0, 4);
          break;
      }
    });
  }

  var out = [];
  out.push(data.fields.map(function(field, index) {
    return pad(field, widths[index]);
  }).join(' '));
  out.push(widths.map(function(width) {
    return '-'.repeat(width);
  }).join(' '));
  data.worlds.forEach(function(world) {
    out.push(data.fields.map(function(field, index) {
      return (world[field] || '')  + ' '.repeat(widths[index] - world[field].length);;
    }).join(' '));
  });

  return out.join('\n') + '\n';
}
