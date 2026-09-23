from pathlib import Path
import gzip
from decimal import Decimal, localcontext
import numpy as np
import json, math, statistics, collections
root=Path(__file__).parent
output=root/'generated';output.mkdir(exist_ok=True)
rows=[json.loads(line) for line in gzip.decompress((root/'diagnostic-inputs.jsonl.gz').read_bytes()).decode().splitlines()]
fixtures={e['context']:e for e in rows if e['kind']=='fixture'}
failures=[e for e in rows if e['kind']=='bfgs-detail']
def terms(x,fixture):
    mu,s,sk=x;y=np.array(fixture['values']);n=len(y);c2=n/(n-1);c3=n*n/((n-1)*(n-2))
    d=y-mu;d1=np.mean(d);d2=np.mean(d*d);d3=np.mean(d*d*d)
    g=np.array([d1,c2*d2-s*s,c3*d3-sk*s**3])
    j=np.array([[-1,0,0],[-2*c2*d1,-2*s,0],[-3*c3*d2,-3*sk*s*s,-s**3]])
    second=np.zeros((3,3,3));second[1,0,0]=2*c2;second[1,1,1]=-2
    second[2,0,0]=6*c3*d1;second[2,1,1]=-6*sk*s;second[2,1,2]=second[2,2,1]=-3*s*s
    return g,j,second
def decimal_q(x,f,w):
    with localcontext() as ctx:
        ctx.prec=70
        z=list(map(Decimal.from_float,x));mu,s,sk=z;vals=list(map(Decimal.from_float,f['values']))
        n=Decimal(len(vals));c2=n/(n-1);c3=n*n/((n-1)*(n-2));d=[v-mu for v in vals]
        g=[sum(d)/n,c2*sum(v*v for v in d)/n-s*s,c3*sum(v*v*v for v in d)/n-sk*s*s*s]
        q=sum(g[i]*Decimal.from_float(w[i][j])*g[j] for i in range(3) for j in range(3))/2
        q+=(sk-Decimal.from_float(f['center']))**2/(2*n*Decimal.from_float(f['mse']))
        return +q
results=[]
for e in failures:
    f=fixtures[e['context']];x=np.array(e['x']);w=np.array(e['weight']);ws=.5*(w+w.T)
    g,j,second=terms(x,f);k=1/(len(f['values'])*f['mse']);pgrad=np.array([0,0,(x[2]-f['center'])*k])
    independent=j.T@ws@g+pgrad
    actualPenalty=np.array(e['g'])-np.array(e['jacobian']).T@w@np.array(e['moments'])
    h=j.T@ws@j+sum((ws@g)[i]*second[i] for i in range(3))+np.diag([0,0,k])
    step=np.linalg.solve(h,-independent);decrease=-.5*independent@step
    q0=decimal_q(e['x'],f,e['weight']);ulp=math.ulp(e['f'])
    deltas=[]
    for si,search in enumerate(e['searches']):
        for trial in search:
            if trial['kind'] not in ['line-trial','zoom-trial'] or not isinstance(trial['value'],(float,int)) or not math.isfinite(trial['value']): continue
            precise=decimal_q(trial['trial'],f,e['weight'])
            # Decimal arithmetic below must retain more than its default 28 digits for tiny differences.
            with localcontext() as ctx:
                ctx.prec=70; trueDelta=float(precise-q0)
            observedDelta=trial['value']-e['f']
            deltas.append(dict(search=si,alpha=trial['alpha'],trueDelta=trueDelta,observedDelta=observedDelta,
                error=observedDelta-trueDelta,armijoAccepted=trial['value']<=trial['armijo'],x=trial['trial']))
    results.append(dict(context=e['context'],passNumber=e['pass'],x=e['x'],objective=e['f'],gradient=e['g'],
        independentGradient=independent.tolist(),gradientError=float(np.max(np.abs(independent-np.array(e['g'])))),
        penaltyGradientError=float(np.max(np.abs(actualPenalty-pgrad))),momentJacobianError=float(np.max(np.abs(j-np.array(e['jacobian'])))),
        hessianEigenvalues=np.linalg.eigvalsh(h).tolist(),predictedNewtonDecrease=float(decrease),objectiveUlp=ulp,
        decreaseInUlps=float(decrease/ulp),newtonStep=step.tolist(),
        maxLineObjectiveDifferenceError=max(abs(t['error']) for t in deltas),
        exactDescendingButRoundedUphill=sum(t['trueDelta']<0 and t['observedDelta']>0 for t in deltas),
        lineTrace=deltas))
norms=[max(map(abs,e['gradient'])) for e in results]
summary=dict(failures=len(results),gradientNormRange=[min(norms),max(norms)],
    maxGradientError=max(e['gradientError'] for e in results),maxPenaltyGradientError=max(e['penaltyGradientError'] for e in results),
    maxMomentJacobianError=max(e['momentJacobianError'] for e in results),
    predictedDecreaseUlpsRange=[min(e['decreaseInUlps'] for e in results),max(e['decreaseInUlps'] for e in results)],
    predictedDecreaseUlpsMedian=statistics.median(e['decreaseInUlps'] for e in results),
    casesWithPredictedDecreaseLessThanOneUlp=sum(e['decreaseInUlps']<1 for e in results),
    casesWithDescendingPointsRoundedUphill=sum(e['exactDescendingButRoundedUphill']>0 for e in results),
    maxLineObjectiveDifferenceError=max(e['maxLineObjectiveDifferenceError'] for e in results),
    minimumHessianEigenvalue=min(min(e['hessianEigenvalues']) for e in results))
(output/'bfgs-cause-results.json').write_text(json.dumps(dict(summary=summary,failures=results),indent=2))
print(json.dumps(summary,indent=2))
for e in sorted(results,key=lambda e:-e['decreaseInUlps'])[:2]+sorted(results,key=lambda e:e['decreaseInUlps'])[:2]:
    print(json.dumps({k:v for k,v in e.items() if k!='lineTrace'},indent=2)); print('Best precise trial',min(e['lineTrace'],key=lambda t:t['trueDelta']))
