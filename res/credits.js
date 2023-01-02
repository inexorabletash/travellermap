// TODO: Unused? Delete?

window.onload = () => {
  const headers = document.querySelectorAll('th'),
        len = headers.length;

  for (let i = 0; i < len; i += 1) {
    window.addEvent(headers[i], 'click', onclick);
  }
};

function onclick(event) {
  const th = event.target,
        tr = th.parentNode,
        tbody = tr.parentNode;
  let i = 0;
  while (tr.children[i] != th) {
    i += 1;
  }
  sortTable(tbody, i);
}

function sortTable(tbody, col) {
  const rows = [];
  let tr;
  while (tbody.children.length > 1) {
    tr = tbody.children[1];
    rows.push(tr);
    tbody.removeChild(tr);
  }

  function compare(a, b) {
    let ta = a.children[col].innerText || a.children[col].textContent,
        tb = b.children[col].innerText || b.children[col].textContent;
    if (ta === String(Number(ta)) && tb === String(Number(tb))) {
      ta = Number(ta);
      tb = Number(tb);
      return ta - tb;
    } else {
      ta = ta.toLowerCase();
      tb = tb.toLowerCase();
    }
    return (ta < tb) ? -1 : (ta > tb) ? 1 : 0;
  }

  rows.sort(compare);

  while (rows.length > 0) {
    tbody.appendChild(rows.shift());
  }
}
