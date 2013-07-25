(function () {
  var list = document.querySelector('#sector');

  var xhr = new XMLHttpRequest();
  xhr.open("GET", "api/universe?requireData=1&accept=application/json", true);
  xhr.onreadystatechange = function () {
    if (xhr.readyState === 4 && xhr.status === 200) {
      populateSectorList(JSON.parse(xhr.responseText));
    }
  };
  xhr.send(null);

  function populateSectorList(universe) {
    var names = [];
    universe.Sectors.forEach(function (sector) {
      var x = sector.X, y = sector.Y, name = sector.Names[0].Text;
      if (Math.abs(x) < 10 && Math.abs(y) < 5) {
        names.push(name);
      }
    });
    names.sort();
    names.forEach(function (name) {
      var option = document.createElement('option');
      option.appendChild(document.createTextNode(name));
      option.value = name;
      list.appendChild(option);
    });
  }

  list.addEventListener("change", function (e) {
    var name = list.value;
    fetchInto("api/sec?sector=" + encodeURIComponent(name) + "&type=legacy&metadata=0&header=0", "#data");
    fetchInto("api/metadata?sector=" + encodeURIComponent(name) + "&accept=text/xml", "#metadata");
  });

  function fetchInto(url, selector) {
    var xhr = new XMLHttpRequest();
    xhr.open("GET", url, true);
    xhr.onreadystatechange = function () {
      if (xhr.readyState === 4 && xhr.status === 200) {
        document.querySelector(selector).value = xhr.responseText;
      }
    };
    xhr.send(null);
  }
}());
