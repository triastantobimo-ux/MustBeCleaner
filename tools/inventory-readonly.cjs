/* Metadata-only inventory. No content reads, hashes, registry access or cleanup.
   Usage: node tools/inventory-readonly.cjs evidence/local-inventory.json
   Logical sizes only. Symlinks and junctions are skipped, arbitrary Windows
   reparse tags are not fully classified by Node. No recursion through links. */
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
function scan(root, limitMs = 150000) {
  const started = Date.now(), groups = new Map(), errors = {}, stack = [root];
  let skippedLinks = 0, complete = true;
  while (stack.length) {
    if (Date.now() - started > limitMs) { complete = false; break; }
    const dir = stack.pop();
    let entries;
    try { entries = fs.readdirSync(dir, {withFileTypes:true}); }
    catch (e) { errors[e.code] = (errors[e.code] || 0) + 1; continue; }
    for (const entry of entries) {
      if (Date.now() - started > limitMs) { complete = false; break; }
      const p = path.join(dir, entry.name);
      try {
        const s = fs.lstatSync(p);
        if (s.isSymbolicLink()) { skippedLinks++; continue; }
        const relative = path.relative(root,p).split(path.sep).slice(0,2).join(path.sep);
        const group = groups.get(relative) || {relative_path:relative,logical_bytes:0,files:0,directories:0};
        if (s.isDirectory()) { group.directories++; stack.push(p); }
        else if (s.isFile()) { group.logical_bytes += s.size; group.files++; }
        groups.set(relative,group);
      } catch (e) { errors[e.code] = (errors[e.code] || 0) + 1; }
    }
  }
  const list = [...groups.values()].sort((a,b)=>b.logical_bytes-a.logical_bytes);
  return {root,complete,seconds:(Date.now()-started)/1000,logical_bytes:list.reduce((n,g)=>n+g.logical_bytes,0),files:list.reduce((n,g)=>n+g.files,0),directories:list.reduce((n,g)=>n+g.directories,0),skipped_links:skippedLinks,errors,groups:list};
}
function main() {
  const output = path.resolve(process.argv[2] || 'evidence/local-inventory.json');
  const home = os.homedir();
  const result = {captured_at:new Date().toISOString(),method:'Metadata-only enumeration. No file content, registry access or cleanup.',caveats:['Logical bytes are not allocated or reclaimable bytes.','Hard links may be counted multiple times.','Skipped symbolic links and junctions; other reparse tags not fully classified.','Live non-atomic scan; errors and timeouts leave unknown coverage.'],drives:{},scans:[]};
  for (const drive of ['C:\\','D:\\']) { const s=fs.statfsSync(drive); result.drives[drive]={total:s.blocks*s.bsize,free:s.bavail*s.bsize}; }
  const roots = process.argv.includes('--targeted')
    ? ['Temp','go-build','claude-mem','AutoClaw 2','Codex','OpenAI'].map(name=>path.join(home,'AppData','Local',name))
    : [path.join(home,'.codex'),path.join(home,'AppData','Local'),path.join(home,'AppData','Roaming')];
  for (const root of roots) {
    const s=scan(root); result.scans.push(s); const {groups,...summary}=s; console.log(JSON.stringify(summary));
    fs.mkdirSync(path.dirname(output),{recursive:true}); fs.writeFileSync(output,JSON.stringify(result,null,2),'utf8');
  }
  console.log(output);
}
main();
