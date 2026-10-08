from pathlib import Path
import json, sys
import numpy as np
root=Path(__file__).parent
output=root/'generated';output.mkdir(exist_ok=True);sys.path.insert(0,str(root/'reference-packages'))
from scipy.optimize import root as find_stationary_point, minimize
source=(root/'outer-map-diagnosis.py').read_text();scope={'__file__':str(root/'outer-map-diagnosis.py')}
exec(compile(source[:source.index('result={}')],str(root/'outer-map-diagnosis.py'),'exec'),scope)
terms=scope['terms'];jac=scope['complex_jacobian'];fixtures=scope['fixtures']
maps=json.loads((output/'outer-map-diagnosis.json').read_text())['cases'];results={}
for name in ['parent','replicate-8','replicate-175','replicate-213','replicate-402']:
    f=fixtures[name];x=np.array(maps[name]['theta']);a=np.array(maps[name]['mapDerivative']);evals,evecs=np.linalg.eig(a)
    q=int(np.argmax(abs(evals)));v=evecs[:,q].real;v/=np.linalg.norm(v);h=1e-6;k=1/(len(f['values'])*f['mse'])
    def solve_inner(previous):
        w=np.linalg.inv(terms(previous,f)[2])
        def score(z):
            g,j,_,_=terms(z,f);return j.T@w@g+np.array([0,0,k*(z[2]-f['center'])])
        fitted=find_stationary_point(score,x,jac=lambda z:jac(score,z),options={'xtol':1e-11})
        return fitted.x,float(max(abs(score(fitted.x))))
    plus,ep=solve_inner(x+h*v);minus,em=solve_inner(x-h*v);observed=(plus-minus)/(2*h)
    # Isolate the weight sensitivity terms without changing any production weighting formula.
    g,j,s,c=terms(x,f);w=np.linalg.inv(s);wg=w@g
    hessian=jac(lambda z:terms(z,f)[1].T@w@terms(z,f)[0]+np.array([0,0,k*(z[2]-f['center'])]),x)
    dc=jac(lambda z:terms(z,f)[3].reshape(9),x).reshape(3,3,3)
    a_model=np.linalg.solve(hessian,np.column_stack([j.T@w@dc[:,:,z]@wg for z in range(3)]))
    results[name]=dict(dominantEigenvalue=float(evals[q].real),finiteDifferenceGain=float(v@observed),
        directionalError=float(np.linalg.norm(observed-a@v)),innerGradientResiduals=[ep,em],
        contributionFromModelCovariance=a_model.tolist(),contributionFromMeanOuterProduct=(a-a_model).tolist())
# An external-package BFGS comparison retains the same outer loop for the clean period-two fixture.
f=fixtures['replicate-8'];theta=np.array(f['initial']);w=np.eye(3);trace=[];k=1/(len(f['values'])*f['mse'])
for iteration in range(1,101):
    def objective(x):
        if x[0]<=0 or x[1]<=0 or not -6<x[2]<6: return np.inf
        g=terms(x,f)[0];return .5*g@w@g+.5*k*(x[2]-f['center'])**2
    def gradient(x):
        g,j,_,_=terms(x,f);return j.T@w@g+np.array([0,0,k*(x[2]-f['center'])])
    fitted=minimize(objective,theta,jac=gradient,method='BFGS',options={'gtol':1e-8,'maxiter':2000})
    new=fitted.x;s=terms(new,f)[2];minimum=float(np.linalg.eigvalsh(s)[0]);assert minimum>0
    w=np.linalg.inv(s);value=objective(new)
    trace.append(dict(iteration=iteration,x=new.tolist(),gradientNorm=float(max(abs(fitted.jac))),status=int(fitted.status),minimumS=minimum,q=value))
    if iteration>1 and (np.linalg.norm(new-theta)<1e-8 or abs(value-oldq)/(abs(oldq)+1e-15)<1e-8 or max(abs(new-theta)/np.maximum(1,abs(theta)))<1e-8): break
    theta=new.copy();oldq=value
results['scipyBfgsSameOuterLoop']=dict(iterations=iteration,trace=trace)
(output/'outer-map-confirmation.json').write_text(json.dumps(results,indent=2))
for name,e in results.items():
    if name!='scipyBfgsSameOuterLoop': print(name,json.dumps({k:v for k,v in e.items() if not k.startswith('contribution')}))
print('SciPy same outer loop',iteration,trace[-2:])
