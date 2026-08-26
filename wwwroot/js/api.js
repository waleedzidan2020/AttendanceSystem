async function apiRequest(url, options={}){
  const headers={...(options.headers||{})};
  if(options.body && !headers['Content-Type']) headers['Content-Type']='application/json';

  const response=await fetch(url,{
    credentials:'include',
    ...options,
    headers
  });

  let result=null;
  try{ result=await response.json(); }catch{}

  if(response.status===401 && url.startsWith('/api/admin/') && !url.includes('/auth/login')){
    window.location='/admin/login';
    throw {status:401,data:result};
  }

  if(!response.ok) throw {status:response.status,data:result};
  return result;
}

function esc(value){return String(value??'').replace(/[&<>'"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]));}
function localTime(value){return value?new Date(value).toLocaleTimeString('ar-EG',{hour:'2-digit',minute:'2-digit'}):'--';}
