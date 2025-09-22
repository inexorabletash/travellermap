import {Universe} from "../build/universe.js";
import path from "node:path";

beforeAll(() => {
    const FILENAME = import.meta.filename;
    const ROOT = path.join(path.dirname(FILENAME), 'data', 'sectors');
    const OVR_ROOT = path.join(path.dirname(FILENAME), 'data', 'override');

    Universe.baseDir = ROOT;
    Universe.OVERRIDE_DIR = OVR_ROOT;
})

test('load default', async () => {
    const universe = await Universe.getUniverse(undefined);
});

test('spin sector exists in default', async () => {
    const universe = await Universe.getUniverse(undefined);
    const spin1 = universe.getSector(-4, -1);
    const spin2 = universe.getSectorByName('Spin');
    const spin3 = universe.getSectorByName('spin');
    const spin4 = universe.getSectorByName('Spinward Marches');

    expect(spin1).toBeDefined();
    expect(spin1).toBe(spin2);
    expect(spin1).toBe(spin3);
    expect(spin1).toBe(spin4);
});

test('spin sector exists in GM', async () => {
    const universe = await Universe.getUniverse('GM');
    const spin1 = universe.getSector(-4, -1);
    const spin2 = universe.getSectorByName('Spin');
    const spin3 = universe.getSectorByName('spin');
    const spin4 = universe.getSectorByName('Spinward Marches');

    expect(spin1).toBeDefined();
    expect(spin1).toBe(spin2);
    expect(spin1).toBe(spin3);
    expect(spin1).toBe(spin4);
});

test('spin sector does not exists in Meta', async () => {
    const universe = await Universe.getUniverse('Meta');
    const spin1 = universe.getSector(-4, -1);
    const spin2 = universe.getSectorByName('Spin');
    const spin3 = universe.getSectorByName('spin');
    const spin4 = universe.getSectorByName('Spinward Marches');

    expect(spin1).toBeUndefined();
    expect(spin1).toBeUndefined();
    expect(spin1).toBeUndefined();
    expect(spin1).toBeUndefined();
});

test('foreven sector exists in default', async () => {
    const universe = await Universe.getUniverse(undefined);
    const spin1 = universe.getSector(-5, -1);
    const spin2 = universe.getSectorByName('Fore');
    const spin3 = universe.getSectorByName('fore');
    const spin4 = universe.getSectorByName('Foreven');

    expect(spin1).toBeDefined();
    expect(spin1).toBe(spin2);
    expect(spin1).toBe(spin3);
    expect(spin1).toBe(spin4);
});

test('foreven sector exists in GM', async () => {
    const universe = await Universe.getUniverse('GM');
    const spin1 = universe.getSector(-5, -1);
    const spin2 = universe.getSectorByName('Fore');
    const spin3 = universe.getSectorByName('fore');
    const spin4 = universe.getSectorByName('Foreven');

    expect(spin1).toBeDefined();
    expect(spin1).toBe(spin2);
    expect(spin1).toBe(spin3);
    expect(spin1).toBe(spin4);
});

test('foreven sector exists in Meta', async () => {
    const universe = await Universe.getUniverse('Meta');
    const spin1 = universe.getSector(-5, -1);
    const spin2 = universe.getSectorByName('Fore');
    const spin3 = universe.getSectorByName('fore');
    const spin4 = universe.getSectorByName('Foreven');

    expect(spin1).toBeDefined();
    expect(spin1).toBe(spin2);
    expect(spin1).toBe(spin3);
    expect(spin1).toBe(spin4);
});

test('spin steel world stats', async() => {
    const universe = await Universe.getUniverse(undefined);
    const spin = universe.getSectorByName('spin');
    const steel = spin?.lookupWorld('1529');
    const steel2 = spin?.lookupWorldByName('steel');
    const steel3 = spin?.lookupWorld(15, 29);
    expect(steel).toBeDefined();
    expect(steel?.name).toEqual('Steel');
    expect(steel).toBe(steel2);
    expect(steel).toBe(steel3);

    expect(steel?.hex).toEqual('1529');
    expect(steel?.allegiance).toEqual('SwCf');
    expect(steel?.uwp).toEqual('E655000-0');
    expect(steel?.ix).toEqual('{ -3 }');
    expect(steel?.ex).toEqual('(200-5)');
    expect(steel?.cx).toEqual('[0000]');
    expect(steel?.bases).toEqual('');
    expect(steel?.pbg).toEqual('024');
    expect(steel?.notes).toEqual(new Set(['Ba', 'Ga', 'Re', 'Lt']));
});

test('spin steel world stats (Test milieu)', async() => {
    const universe = await Universe.getUniverse('Test');
    const spin = universe.getSectorByName('spin');
    const steel = spin?.lookupWorld('1529');
    expect(steel).toBeDefined();
    expect(steel?.name).toEqual('Steel');

    expect(steel?.hex).toEqual('1529');
    expect(steel?.allegiance).toEqual('ImDd');
    expect(steel?.uwp).toEqual('E655361-8');
    expect(steel?.ix).toEqual('{ -3 }');
    expect(steel?.ex).toEqual('(200-5)');
    expect(steel?.cx).toEqual('[0000]');
    expect(steel?.bases).toEqual('');
    expect(steel?.pbg).toEqual('424');
    expect(steel?.notes).toEqual(new Set(['Ga']));
});

test('spin sector routes and borders', async () => {
    const universe = await Universe.getUniverse(undefined);
    const spin1 = universe.getSector(-4, -1);

    expect(spin1).toBeDefined();
    expect(spin1?.routes).toEqual([{"start":"1106","end":"1006"},{"start":"1106","end":"1005"},{"start":"1106","end":"1204"},{"start":"1106","end":"1307"},{"start":"1307","end":"1705"},{"start":"1826","end":"2228"},{"start":"1705","end":"1904"},{"start":"1904","end":"1903"},{"start":"1903","end":"2202"},{"start":"1904","end":"2005"},{"start":"2005","end":"2007"},{"start":"2007","end":"1910"},{"start":"1910","end":"1810"},{"start":"1810","end":"1711"},{"start":"1711","end":"1413"},{"start":"1413","end":"1116"},{"start":"1116","end":"1118"},{"start":"1910","end":"1912"},{"start":"1912","end":"1815"},{"start":"1815","end":"1719"},{"start":"1719","end":"1920"},{"start":"1920","end":"2319"},{"start":"2319","end":"2417"},{"start":"2417","end":"2716"},{"start":"2716","end":"2814"},{"start":"2814","end":"2712"},{"start":"2712","end":"2510"},{"start":"2510","end":"2410"},{"start":"2814","end":"2913"},{"start":"2913","end":"3010"},{"start":"3010","end":"3110"},{"start":"3110","end":"3209"},{"start":"3209","end":"0208","endOffsetX":1},{"start":"2913","end":"3212"},{"start":"3212","end":"0311","endOffsetX":1},{"start":"2319","end":"2418"},{"start":"2418","end":"2621"},{"start":"2621","end":"2323"},{"start":"2323","end":"2124"},{"start":"2124","end":"1824"},{"start":"1824","end":"1526"},{"start":"1526","end":"1525","allegiance":"SwCf"},{"start":"1525","end":"1524","allegiance":"SwCf"},{"start":"1524","end":"1523","allegiance":"SwCf"},{"start":"1523","end":"1522","allegiance":"SwCf"},{"start":"1524","end":"1424","allegiance":"SwCf"},{"start":"1424","end":"1324","allegiance":"SwCf"},{"start":"1324","end":"1223","allegiance":"SwCf"},{"start":"1223","end":"1123","allegiance":"SwCf"},{"start":"1123","end":"1022","allegiance":"SwCf"},{"start":"1022","end":"0922","allegiance":"SwCf"},{"start":"0922","end":"0921","allegiance":"SwCf"},{"start":"1424","end":"1325","allegiance":"SwCf"},{"start":"1325","end":"1225","allegiance":"SwCf"},{"start":"1225","end":"1126","allegiance":"SwCf"},{"start":"1126","end":"1026","allegiance":"SwCf"},{"start":"1026","end":"0927","allegiance":"SwCf"},{"start":"1526","end":"1329"},{"start":"1329","end":"0930"},{"start":"0930","end":"0732"},{"start":"0732","end":"0534"},{"start":"0534","end":"0433"},{"start":"0433","end":"0333"},{"start":"0333","end":"0133"},{"start":"0534","end":"0538"},{"start":"0538","end":"0139"},{"start":"2124","end":"2125"},{"start":"2125","end":"1826"},{"start":"2125","end":"2327"},{"start":"2327","end":"2228"},{"start":"2228","end":"2231"},{"start":"2231","end":"2334"},{"start":"2334","end":"2336"},{"start":"2336","end":"2036"},{"start":"2036","end":"1937"},{"start":"1937","end":"1737"},{"start":"1737","end":"1537"},{"start":"2036","end":"1938"},{"start":"2036","end":"2138"},{"start":"2138","end":"2140"},{"start":"2140","end":"2102","endOffsetY":1},{"start":"2336","end":"2536"},{"start":"2536","end":"2537"},{"start":"2537","end":"2637"},{"start":"2637","end":"2739"},{"start":"2739","end":"2839"},{"start":"2839","end":"2940"},{"start":"2839","end":"3139"},{"start":"2637","end":"2936"},{"start":"2936","end":"3235"},{"start":"3235","end":"3032"},{"start":"3032","end":"2733"},{"start":"2733","end":"2536"},{"start":"2733","end":"2334"},{"start":"3032","end":"3030"},{"start":"3030","end":"3029"},{"start":"3029","end":"2927"},{"start":"2927","end":"2828"},{"start":"2927","end":"3025"},{"start":"3025","end":"2726"},{"start":"2726","end":"2324"},{"start":"2323","end":"2324"},{"start":"3025","end":"3124"},{"start":"3124","end":"3324"},{"start":"3003","end":"3103"},{"start":"3103","end":"3202"},{"start":"3202","end":"0201","endOffsetX":1},{"start":"0001","end":"0103","allegiance":"ZhCo"},{"start":"0103","end":"0304","allegiance":"ZhCo"},{"start":"0304","end":"0307","allegiance":"ZhCo"},{"start":"0307","end":"0608","allegiance":"ZhCo"},{"start":"0608","end":"0610","allegiance":"ZhCo"},{"start":"0610","end":"0712","allegiance":"ZhCo"},{"start":"0712","end":"0614","allegiance":"ZhCo"},{"start":"0614","end":"0412","allegiance":"ZhCo"},{"start":"0303","end":"0304","allegiance":"ZhCo"},{"start":"0304","end":"0705","allegiance":"ZhCo"},{"start":"0705","end":"0904","allegiance":"ZhCo"},{"start":"0904","end":"1103","allegiance":"ZhCo"},{"start":"1103","end":"1402","allegiance":"ZhCo"},{"start":"0421","end":"0223","allegiance":"DaCf"},{"start":"0223","end":"0325","allegiance":"DaCf"},{"start":"0325","end":"0426","allegiance":"DaCf"},{"start":"0426","end":"0527","allegiance":"DaCf"},{"start":"0527","end":"0727","allegiance":"DaCf"},{"start":"0325","end":"0624","allegiance":"DaCf"},{"start":"0624","end":"0724","allegiance":"DaCf"}]);
    expect(spin1?.borders).toEqual([{"allegiance":"DaCf","labelPosition":423,"wrapLabel":true,"hexes":["0223","0323","0422","0421","0522","0621","0721","0821","0822","0723","0724","0725","0726","0727","0728","0627","0527","0427","0426","0326","0325","0224","0223"]},{"allegiance":"SwCf","labelPosition":1327,"wrapLabel":true,"hexes":["0620","0720","0820","0921","1021","1121","1221","1322","1421","1522","1523","1524","1525","1526","1626","1627","1628","1529","1528","1427","1327","1227","1128","1129","1130","1129","1028","0928","0927","0926","0925","0924","0923","0922","0921","0820","0720","0620"]},{"allegiance":"ZhIN","label":"Zhodani Consulate","labelPosition":205,"wrapLabel":true,"hexes":["0000","0100","0200","0101","0102","0202","0303","0403","0504","0604","0705","0804","0904","1003","1103","1102","1201","1302","1401","1402","1303","1202","1103","1003","0904","0804","0705","0604","0505","0506","0507","0508","0608","0609","0610","0611","0712","0612","0613","0614","0514","0413","0412","0512","0511","0610","0609","0608","0508","0407","0307","0206","0106","0006","0005","0004","0003","0002","0001","0000"]},{"allegiance":"ImDd","label":"Third Imperium","labelPosition":2021,"wrapLabel":true,"hexes":["1005","1105","1204","1305","1405","1505","1604","1704","1803","1802","1903","2002","2102","2201","2302","2402","2502","2601","2701","2801","2902","3001","3102","3201","3302","3303","3304","3305","3306","3307","3308","3309","3310","3311","3312","3313","3314","3315","3316","3317","3318","3319","3320","3321","3322","3323","3324","3325","3326","3327","3328","3329","3330","3331","3332","3333","3334","3335","3336","3337","3338","3339","3340","3341","3241","3141","3041","2941","2841","2741","2641","2541","2441","2341","2241","2141","2041","1941","1840","1839","1739","1738","1637","1537","1636","1736","1835","1834","1833","1733","1832","1831","1731","1730","1729","1728","1727","1826","1825","1824","1823","1822","1821","1721","1620","1520","1419","1320","1219","1119","1019","1020","1019","1018","1118","1117","1116","1215","1214","1314","1313","1412","1512","1611","1711","1810","1910","1909","1808","1708","1607","1507","1407","1307","1207","1107","1006","1005"]},{"allegiance":"ImDd","label":"Third Imperium","wrapLabel":true,"hexes":["1329","1429","1430","1330","1329"]},{"allegiance":"ImDd","label":"Third Imperium","labelPosition":335,"wrapLabel":true,"hexes":["0133","0232","0332","0432","0533","0632","0732","0733","0633","0534","0535","0635","0636","0737","0738","0638","0539","0438","0339","0238","0139","0138","0137","0136","0135","0134","0133"]},{"allegiance":"ImDd","label":"Third Imperium","wrapLabel":true,"hexes":["930"]}])
});

test('spin sector routes and borders (Test Milieu)', async () => {
    const universe = await Universe.getUniverse('Test');
    const spin1 = universe.getSector(-4, -1);

    expect(spin1).toBeDefined();
    console.log(JSON.stringify(spin1?.routes));
    console.log(JSON.stringify(spin1?.borders));
    expect(spin1?.routes).toEqual([{"start":"1106","end":"1006"},{"start":"1106","end":"1005"},{"start":"1106","end":"1204"},{"start":"1106","end":"1307"},{"start":"1307","end":"1705"},{"start":"1826","end":"2228"},{"start":"1705","end":"1904"},{"start":"1904","end":"1903"},{"start":"1903","end":"2202"},{"start":"1904","end":"2005"},{"start":"2005","end":"2007"},{"start":"2007","end":"1910"},{"start":"1910","end":"1810"},{"start":"1810","end":"1711"},{"start":"1711","end":"1413"},{"start":"1413","end":"1116"},{"start":"1116","end":"1118"},{"start":"1910","end":"1912"},{"start":"1912","end":"1815"},{"start":"1815","end":"1719"},{"start":"1719","end":"1920"},{"start":"1920","end":"2319"},{"start":"2319","end":"2417"},{"start":"2417","end":"2716"},{"start":"2716","end":"2814"},{"start":"2814","end":"2712"},{"start":"2712","end":"2510"},{"start":"2510","end":"2410"},{"start":"2814","end":"2913"},{"start":"2913","end":"3010"},{"start":"3010","end":"3110"},{"start":"3110","end":"3209"},{"start":"3209","end":"0208","endOffsetX":1},{"start":"2913","end":"3212"},{"start":"3212","end":"0311","endOffsetX":1},{"start":"2319","end":"2418"},{"start":"2418","end":"2621"},{"start":"2621","end":"2323"},{"start":"2323","end":"2124"},{"start":"2124","end":"1824"},{"start":"1824","end":"1526"},{"start":"1523","end":"1522","allegiance":"SwCf"},{"start":"1223","end":"1123","allegiance":"SwCf"},{"start":"1123","end":"1022","allegiance":"SwCf"},{"start":"1022","end":"0922","allegiance":"SwCf"},{"start":"0922","end":"0921","allegiance":"SwCf"},{"start":"1126","end":"1026","allegiance":"SwCf"},{"start":"1026","end":"0927","allegiance":"SwCf"},{"start":"1526","end":"1329"},{"start":"1329","end":"0930"},{"start":"0930","end":"0732"},{"start":"0732","end":"0534"},{"start":"0534","end":"0433"},{"start":"0433","end":"0333"},{"start":"0333","end":"0133"},{"start":"0534","end":"0538"},{"start":"0538","end":"0139"},{"start":"2124","end":"2125"},{"start":"2125","end":"1826"},{"start":"2125","end":"2327"},{"start":"2327","end":"2228"},{"start":"2228","end":"2231"},{"start":"2231","end":"2334"},{"start":"2334","end":"2336"},{"start":"2336","end":"2036"},{"start":"2036","end":"1937"},{"start":"1937","end":"1737"},{"start":"1737","end":"1537"},{"start":"2036","end":"1938"},{"start":"2036","end":"2138"},{"start":"2138","end":"2140"},{"start":"2140","end":"2102","endOffsetY":1},{"start":"2336","end":"2536"},{"start":"2536","end":"2537"},{"start":"2537","end":"2637"},{"start":"2637","end":"2739"},{"start":"2739","end":"2839"},{"start":"2839","end":"2940"},{"start":"2839","end":"3139"},{"start":"2637","end":"2936"},{"start":"2936","end":"3235"},{"start":"3235","end":"3032"},{"start":"3032","end":"2733"},{"start":"2733","end":"2536"},{"start":"2733","end":"2334"},{"start":"3032","end":"3030"},{"start":"3030","end":"3029"},{"start":"3029","end":"2927"},{"start":"2927","end":"2828"},{"start":"2927","end":"3025"},{"start":"3025","end":"2726"},{"start":"2726","end":"2324"},{"start":"2323","end":"2324"},{"start":"3025","end":"3124"},{"start":"3124","end":"3324"},{"start":"3003","end":"3103"},{"start":"3103","end":"3202"},{"start":"3202","end":"0201","endOffsetX":1},{"start":"0001","end":"0103","allegiance":"ZhCo"},{"start":"0103","end":"0304","allegiance":"ZhCo"},{"start":"0304","end":"0307","allegiance":"ZhCo"},{"start":"0307","end":"0608","allegiance":"ZhCo"},{"start":"0608","end":"0610","allegiance":"ZhCo"},{"start":"0610","end":"0712","allegiance":"ZhCo"},{"start":"0712","end":"0614","allegiance":"ZhCo"},{"start":"0614","end":"0412","allegiance":"ZhCo"},{"start":"0303","end":"0304","allegiance":"ZhCo"},{"start":"0304","end":"0705","allegiance":"ZhCo"},{"start":"0705","end":"0904","allegiance":"ZhCo"},{"start":"0904","end":"1103","allegiance":"ZhCo"},{"start":"1103","end":"1402","allegiance":"ZhCo"},{"start":"0421","end":"0223","allegiance":"DaCf"},{"start":"0223","end":"0325","allegiance":"DaCf"},{"start":"0325","end":"0426","allegiance":"DaCf"},{"start":"0426","end":"0527","allegiance":"DaCf"},{"start":"0527","end":"0727","allegiance":"DaCf"},{"start":"0325","end":"0624","allegiance":"DaCf"},{"start":"0624","end":"0724","allegiance":"DaCf"},{"start":"1223","end":"1523","allegiance":"SwCf"},{"start":"1223","end":"1126","allegiance":"SwCf"},{"start":"1324","end":"1424","allegiance":"BwCf"},{"start":"1424","end":"1524","allegiance":"BwCf"},{"start":"1424","end":"1325","allegiance":"BwCf"},{"start":"1325","end":"1225","allegiance":"BwCf"},{"start":"1329","end":"1727","allegiance":"ImDd"},{"start":"1727","end":"1826","allegiance":"ImDd"},{"start":"1826","end":"1824","allegiance":"Im"}]);
    expect(spin1?.borders).toEqual([{"allegiance":"ZhIN","label":"Zhodani Consulate","labelPosition":205,"wrapLabel":true,"hexes":["0000","0100","0200","0101","0102","0202","0303","0403","0504","0604","0705","0804","0904","1003","1103","1102","1201","1302","1401","1402","1303","1202","1103","1003","0904","0804","0705","0604","0505","0506","0507","0508","0608","0609","0610","0611","0712","0612","0613","0614","0514","0413","0412","0512","0511","0610","0609","0608","0508","0407","0307","0206","0106","0006","0005","0004","0003","0002","0001","0000"]},{"allegiance":"ImDd","label":"Third Imperium","labelPosition":335,"wrapLabel":true,"hexes":["0133","0232","0332","0432","0533","0632","0732","0733","0633","0534","0535","0635","0636","0737","0738","0638","0539","0438","0339","0238","0139","0138","0137","0136","0135","0134","0133"]},{"allegiance":"ImDd","label":"Third Imperium","wrapLabel":true,"hexes":["930"]},{"allegiance":"SwCf","labelPosition":"1024","wrapLabel":true,"hexes":["1021","1121","1221","1322","1421","1522","1523","1423","1323","1223","1224","1125","1126","1127","1128","1129","1130","1129","1028","0928","0927","0926","0925","0924","0923","0922","0921","1021"],"label":"Sword Worlds Confederation"},{"allegiance":"DaCf","labelPosition":423,"wrapLabel":true,"hexes":["0223","0323","0422","0421","0521","0620","0720","0820","0821","0822","0723","0724","0725","0726","0727","0728","0627","0527","0427","0426","0326","0325","0224","0223"],"label":"Darrien Confereration"},{"hexes":["1324","1424","1524","1424","1325","1225","1325","1324"],"allegiance":"BwCf","label":"Border Worlds","labelPosition":"1423","wrapLabel":true},{"allegiance":"ImDd","label":"Third Imperium","labelPosition":2021,"wrapLabel":true,"hexes":["1005","1105","1204","1305","1405","1505","1604","1704","1803","1802","1903","2002","2102","2201","2302","2402","2502","2601","2701","2801","2902","3001","3102","3201","3302","3303","3304","3305","3306","3307","3308","3309","3310","3311","3312","3313","3314","3315","3316","3317","3318","3319","3320","3321","3322","3323","3324","3325","3326","3327","3328","3329","3330","3331","3332","3333","3334","3335","3336","3337","3338","3339","3340","3341","3241","3141","3041","2941","2841","2741","2641","2541","2441","2341","2241","2141","2041","1941","1840","1839","1739","1738","1637","1537","1636","1736","1835","1834","1833","1733","1832","1831","1731","1730","1729","1628","1529","1429","1430","1330","1329","1328","1327","1426","1526","1625","1624","1724","1823","1822","1821","1721","1620","1520","1419","1320","1219","1119","1019","1020","1019","1018","1118","1117","1116","1215","1214","1314","1313","1412","1512","1611","1711","1810","1910","1909","1808","1708","1607","1507","1407","1307","1207","1107","1006","1005"]}])
});

