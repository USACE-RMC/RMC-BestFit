from pathlib import Path
import gzip
import json,sys,collections
import numpy as np
root=Path(__file__).parent
output=root/'generated';output.mkdir(exist_ok=True);sys.path.insert(0,str(root/'reference-packages'))
import scipy
from scipy.optimize import root as find_stationary_point
rows=[json.loads(line) for line in gzip.decompress((root/'diagnostic-inputs.jsonl.gz').read_bytes()).decode().splitlines()]
fixtures={e['context']:e for e in rows if e['kind']=='fixture'}
fits={e['context']:e for e in rows if e['kind']=='fit-end'}
passes=collections.defaultdict(list)
for e in rows:
    if e['kind']=='pass-end':passes[e['context']].append(e)
def terms(x,f):
    mu,s,gamma=x;values=np.array(f['values']);n=len(values);c2=n/(n-1);c3=n*n/((n-1)*(n-2))
    d=values-mu;d1=np.mean(d);d2=np.mean(d*d);d3=np.mean(d*d*d)
    g=np.array([d1,c2*d2-s*s,c3*d3-gamma*s**3]);j=np.array([[-1,0,0],[-2*c2*d1,-2*s,0],[-3*c3*d2,-3*gamma*s*s,-s**3]])
    m2=s*s;m3=gamma*s**3;m4=s**4*(3+1.5*gamma**2);m5=s**5*gamma*(10+3*gamma**2);m6=s**6*(15+32.5*gamma**2+7.5*gamma**4)
    c=np.array([[m2,m3,m4],[m3,m4-m2*m2,m5-m2*m3],[m4,m5-m2*m3,m6-m3*m3]])
    return g,j,c-np.outer(g,g),c
def complex_jacobian(fun,x):
    result=[]
    for k in range(len(x)):
        z=np.array(x,dtype=complex);z[k]+=1E-24j
        result.append(np.imag(fun(z))/1E-24)
    return np.array(result).T
result={}
for name,f in fixtures.items():
    y=np.array(f['values']);n=len(y);mu=np.mean(y);s=np.std(y,ddof=1);sk=n*n/((n-1)*(n-2))*np.mean((y-mu)**3)/s**3;k=1/(n*f['mse'])
    def score(x):
        g,j,cov,_=terms(x,f)
        return j.T@np.linalg.solve(cov,g)+np.array([0,0,k*(x[2]-f['center'])])
    start=[mu,s,sk]
    fit=find_stationary_point(score,start,jac=lambda x:complex_jacobian(score,x),options={'xtol':1E-11})
    theta=fit.x;g,j,cov,c=terms(theta,f);residual=max(abs(score(theta)));eigS=np.linalg.eigvalsh(cov)
    valid=np.all(np.isfinite(theta)) and theta[0]>0 and theta[1]>0 and eigS[0]>0 and residual<1E-9
    entry=dict(rootFound=bool(valid),theta=theta.tolist(),scoreResidual=float(residual),minimumS=float(eigS[0]),
        penaltyCenter=f['center'],outerPasses=fits[name]['passes'],converged=fits[name]['converged'])
    if valid:
        w=np.linalg.inv(cov);wg=w@g
        # Partial x derivative of J(x)' W(a) g(x) + grad P(x), holding the previous weight fixed.
        def fixed_weight_score(x):
            gx,jx,_,_=terms(x,f)
            return jx.T@w@gx+np.array([0,0,k*(x[2]-f['center'])])
        h=complex_jacobian(fixed_weight_score,theta)
        dS=complex_jacobian(lambda x:terms(x,f)[2].reshape(9),theta).reshape(3,3,3)
        b=np.column_stack([-j.T@w@dS[:,:,z]@wg for z in range(3)])
        derivative=-np.linalg.solve(h,b);eigenvalues=np.linalg.eigvals(derivative)
        entry.update(mapEigenvalues=[dict(real=float(v.real),imag=float(v.imag)) for v in eigenvalues],
            spectralRadius=float(max(abs(eigenvalues))),mapDerivative=derivative.tolist(),
            momentMismatch=float(g@np.linalg.solve(c,g)),hessianEigenvalues=np.linalg.eigvalsh(h).tolist())
    if len(passes[name])>=3:
        tail=passes[name][-3:]
        entry.update(lastStep=float(np.linalg.norm(np.array(tail[-1]['values'])-tail[-2]['values'])),
            twoPassReturnDistance=float(np.linalg.norm(np.array(tail[-1]['values'])-tail[-3]['values'])),
            tail=[dict(passNumber=e['pass'],values=e['values'],q=e['q']) for e in tail])
    result[name]=entry
allboot=[e for c,e in result.items() if c!='parent'];capped=[e for e in allboot if e['outerPasses']==100]
summary=dict(scipyVersion=scipy.__version__,bootstrapStationaryPointsFound=sum(e['rootFound'] for e in allboot),
    bootstrapUnstableFixedPoints=sum(e.get('spectralRadius',0)>1 for e in allboot),
    cappedAt100=len(capped),cappedUnstableFixedPoints=sum(e.get('spectralRadius',0)>1 for e in capped),
    cappedRootsNotFound=sum(not e['rootFound'] for e in capped),
    cappedMacroscopicAlternation=sum(e.get('lastStep',0)>1E-4 and e.get('twoPassReturnDistance',1)<1E-5 for e in capped),
    cappedSmallAmplitude=sum(e.get('lastStep',0)<=1E-4 for e in capped))
(output/'outer-map-diagnosis.json').write_text(json.dumps(dict(summary=summary,cases=result),indent=2))
print(json.dumps(summary,indent=2))
for name in ['parent','replicate-0','replicate-8','replicate-9','replicate-213','replicate-402']:
    print(name,json.dumps(result[name],indent=2))
