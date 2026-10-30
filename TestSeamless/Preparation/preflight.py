import sys,json,pathlib,hashlib,decimal,datetime,re
root=pathlib.Path(sys.argv[1]); tables=json.loads(pathlib.Path(sys.argv[2]).read_text(encoding='utf-8-sig')); counts={}; hashes={}
for table in tables:
 p=root/table['file']
 if not p.exists():
  if table['name']=='TRADE_REQUEST': counts[table['name']]=0;continue
  raise Exception('missing '+str(p))
 h=hashlib.sha256(); n=0
 with p.open('rb') as stream:
  for raw in stream:
   h.update(raw); line=raw.decode('utf-8').rstrip('\r\n')
   if not line:continue
   fields=line.split('|'); columns=table['columns']
   if len(fields)==len(columns)+1 and fields[-1]=='':fields.pop()
   if len(fields)!=len(columns):raise ValueError(f"{table['name']}:{n+1} field count {len(fields)} expected {len(columns)}")
   for value,col in zip(fields,columns):
    typ=col['type']
    if not value and col['nullable']:continue
    try:
     if typ.startswith('varchar'):
      if len(value.encode('utf-8'))>int(re.search(r'\d+',typ).group()): raise ValueError('string too long')
     elif typ.startswith('varbinary'):
      if len(value.encode('utf-8'))>100000:raise ValueError('blob too long')
     elif typ in ('int','bigint'):
      v=int(value); limit=2**(31 if typ=='int' else 63)
      if not -limit<=v<limit: raise ValueError('overflow')
     elif typ=='bit':
      if value not in ('0','1'):raise ValueError('not bit')
     elif typ.startswith('decimal'):
      precision,scale=map(int,re.findall(r'\d+',typ)+(['0'] if ',' not in typ else [])); v=decimal.Decimal(value)
      if v!=v.quantize(decimal.Decimal(1).scaleb(-scale)) or abs(v)>=10**(precision-scale):raise ValueError('decimal out of range or precision')
     elif typ=='date': datetime.date.fromisoformat(value)
     elif typ.startswith('datetime'): datetime.datetime.fromisoformat(value)
     else:raise ValueError('unknown type')
    except Exception as e:raise ValueError(f"{table['name']}:{n+1} {col['name']} {typ}: {e}") from e
   n+=1
 counts[table['name']]=n;hashes[table['file']]=h.hexdigest();print(table['name'],n,flush=True)
pathlib.Path(sys.argv[3]).write_text(json.dumps({'counts':counts,'outputSha256':hashes},indent=2),encoding='utf-8')
