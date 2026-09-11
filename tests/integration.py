"""Deterministic HTTP contract tests. No external provider requests are made."""
import http.cookiejar,json,os,socket,subprocess,tempfile,time,urllib.request,urllib.error
from pathlib import Path
root=Path(__file__).resolve().parents[1]
with socket.socket() as sock:
 sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
base=f'http://127.0.0.1:{port}'
opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
def request(path,body=None,code=200,client=opener):
 req=urllib.request.Request(base+path,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'})
 try:
  with client.open(req,timeout=30) as res: status=res.status; data=res.read()
 except urllib.error.HTTPError as ex: status=ex.code;data=ex.read()
 assert status==code,(path,status,data.decode()[:500])
 return json.loads(data) if data else None
with tempfile.TemporaryDirectory() as directory:
 env={**os.environ,'ASPNETCORE_URLS':base,'ASPNETCORE_ENVIRONMENT':'Testing','TestFixtures':'true','DATA_DIR':directory}
 with open(Path(directory)/'server.log','w+') as log:
  process=subprocess.Popen([os.environ.get('DOTNET','dotnet'),str(root/'src/bin/Release/net10.0/App.dll'),'--contentRoot',str(root/'src')],env=env,stdout=log,stderr=log,cwd=root)
  try:
   for _ in range(100):
    try:
     assert request('/api/health')['mode']=='test fixtures';break
    except (OSError,AssertionError): time.sleep(.1)
   else: raise RuntimeError('Server did not start')
   f=request('/api/market');assert f['status']=='test fixtures' and len(f['instruments'])==2
   assert f['instruments'][0]['spread']==1
   c=request('/api/candles?symbol=BTC%2FUSD');assert c['completedOnly'] and len(c['candles'])==2
   request('/api/candles?symbol=NOPE',code=400)
   saved=request('/api/recordings',{});replay=request('/api/recordings/'+saved['id'])
   assert replay['frames'][0]['instruments']==f['instruments'] and replay['source']=='test fixtures'
   assert saved['id'] in request('/api/recordings')
   request('/api/recordings/not-an-id',code=404)
   print('HTTP feed, symbol validation, completed candles and persisted replay equivalence passed.')
  finally:
   process.terminate()
   try:process.wait(timeout=10)
   except subprocess.TimeoutExpired:process.kill();process.wait()
