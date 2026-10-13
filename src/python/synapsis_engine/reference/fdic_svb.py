"""FDIC: the SVB and Republic First ranks on lens A (uninsured share and unrealized loss to equity) at their year-end report.
Public FDIC API, no key. Run: python fdic_svb.py"""
import json, urllib.request, urllib.parse, statistics as st, sys
F="CERT,NAME,STALP,DEP,DEPUNINS,EQ,SCAA,SCAF,SCHA,SCHF"
def get(rep):
    q=urllib.parse.urlencode({"filters":f"REPDTE:{rep}","fields":F,"limit":10000})
    req=urllib.request.Request("https://api.fdic.gov/banks/financials?"+q, headers={"User-Agent":"synapsis-check"})
    return [d["data"] for d in json.load(urllib.request.urlopen(req,timeout=120))["data"]]
def rz(xs):
    m=st.median(xs); mad=st.median([abs(x-m) for x in xs])*1.4826
    return lambda x:(x-m)/mad
for rep,cert,name in (("20221231",24735,"SVB"),("20221231",57053,"Signature"),("20231231",27332,"Republic First")):
    rows=get(rep); ok=[]
    for r in rows:
        try:
            dep,eq=float(r["DEP"]),float(r["EQ"])
            if dep<=0 or eq<=0 or r["DEPUNINS"] is None: continue
            unins=float(r["DEPUNINS"])/dep
            loss=((float(r["SCAA"] or 0)-float(r["SCAF"] or 0))+(float(r["SCHA"] or 0)-float(r["SCHF"] or 0)))/eq
            ok.append((int(r["CERT"]),r["NAME"],unins,loss))
        except (TypeError,ValueError,KeyError): pass
    zu=rz([o[2] for o in ok]); zl=rz([o[3] for o in ok])
    scored=sorted(((min(zu(u),zl(l)),c,n,u,l) for c,n,u,l in ok),reverse=True)
    rank=[i for i,s in enumerate(scored,1) if s[1]==cert]
    print(rep,name,"banks",len(ok),"rank",rank, [ (round(s[0],2),s[2][:28],round(s[3],3),round(s[4],3)) for s in scored if s[1]==cert])
    print("  top8:",[ (s[2][:24],round(s[0],2)) for s in scored[:8]])
