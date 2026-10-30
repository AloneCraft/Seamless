"""Read-only ScriptDom dependency inventory; not a SQL compatibility test."""
import argparse,json,pathlib,re,sys,xml.etree.ElementTree as ET
PACKAGE="microsoft.sqlserver.transactsql.scriptdom"
SKIP={".git",".claude",".vs",".agents",".codex","Work","Artifacts","bin","obj","node_modules"}
def inspect_text(text):
    return [i for i,line in enumerate(text.splitlines(),1) if "Microsoft.SqlServer.TransactSql.ScriptDom" in line or re.search(r"\bTSql[0-9]+Parser\b",line)]
def inspect_project(text):
    return [n.attrib.get("Include",n.attrib.get("Update","")) for n in ET.fromstring(text).iter() if n.tag.split("}")[-1]=="PackageReference" and n.attrib.get("Include",n.attrib.get("Update","")).lower()==PACKAGE]
def inspect_assets(text):
    return [key for key in json.loads(text).get("libraries",{}) if key.split("/")[0].lower()==PACKAGE]
def run(root,assets):
    hits=[];errors=[]
    def walk(folder):
        for p in sorted(folder.iterdir()):
            if p.is_symlink() or p.is_junction():continue
            if p.is_dir():
                if p.name not in SKIP:yield from walk(p)
            elif p.suffix in {".cs",".csproj",".props",".targets"}:yield p
    for p in walk(root):
        try:
            text=p.read_text(encoding="utf-8-sig")
            if p.suffix==".cs":
                lines=inspect_text(text)
                if lines:hits.append({"path":p.relative_to(root).as_posix(),"kind":"SourceTextCandidate","lines":lines})
            else:
                packages=inspect_project(text)
                if packages:hits.append({"path":p.relative_to(root).as_posix(),"kind":"DirectPackage","packages":packages})
        except (OSError,ValueError,ET.ParseError,UnicodeError) as e:errors.append({"path":str(p),"error":str(e)})
    asset_results=[]
    for path in assets:
        try:asset_results.append({"path":str(path),"dependencies":inspect_assets(path.read_text(encoding="utf-8-sig"))})
        except (OSError,ValueError,UnicodeError) as e:errors.append({"path":str(path),"error":str(e)})
    remaining=bool(hits) or any(x["dependencies"] for x in asset_results)
    return {"sourceRoot":str(root),"hits":hits,"candidateFiles":len(hits),"assets":asset_results,"assetsStatus":"Inspected" if assets else "NotInspected","errors":errors,"dependenciesRemain":remaining,"runtimeCompatibility":"NotTested"}
def main():
    ap=argparse.ArgumentParser();ap.add_argument("--root",type=pathlib.Path,default=pathlib.Path.cwd());ap.add_argument("--assets",type=pathlib.Path,action="append",default=[]);ap.add_argument("--require-clean",action="store_true");args=ap.parse_args()
    root=args.root.resolve()
    if not root.is_dir():ap.error("root must be an existing directory")
    result=run(root,args.assets);print(json.dumps(result,ensure_ascii=False,indent=2))
    if result["errors"]:return 2
    if args.require_clean:
        if result["dependenciesRemain"]:return 1
        if not args.assets:return 2
    return 0
if __name__=="__main__":sys.exit(main())
