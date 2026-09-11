const $=id=>document.getElementById(id);
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
async function api(path,body){const r=await fetch(path,{method:body===undefined?'GET':'POST',headers:body===undefined?{}:{'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});const data=await r.json();if(!r.ok)throw Error(data.error||data.title||'Request failed');return data;}
function status(message,error=false){$('status').textContent=message;$('status').classList.toggle('error',error)}
async function action(button,work){button.disabled=true;try{await work()}catch(e){status(e.message,true)}finally{button.disabled=false}}
function download(data,name){const blob=new Blob([JSON.stringify(data,null,2)],{type:'application/json'});const u=URL.createObjectURL(blob);const a=document.createElement('a');a.href=u;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(u),1000)}
api('/api/health').then(x=>{$('mode').textContent=x.mode}).catch(e=>status(e.message,true));
