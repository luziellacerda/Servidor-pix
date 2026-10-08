"""Compose R79 against frozen R77 Java without changing prior sources."""
from pathlib import Path
import importlib.util,json,hashlib
ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT.parent/'station-multiplayer-r77-20261008'
text=(BASE/'recipes/build_java.py').read_text('utf8')
text=text.replace('the reviewed R77 overlays over the frozen R76','the reviewed R79 overlays over the frozen R77')
text=text.replace("PREVIOUS=SNAPSHOT.parent/'station-pump-wakeup-r76-20261008'","PREVIOUS=SNAPSHOT.parent/'station-multiplayer-r77-20261008'")
text=text.replace("station-multiplayer-r77-20261008\\java-build-01","station-room-bootstrap-r79-20261008\\java-build-01")
text=text.replace('1a04582a02804ecbe4b075dd487118a171b0b0f305dbb8884015bbede09b25d7','846f7f8556c42816a9465bf1338f8ebf0a29a57162b04e8e5645e1fc9b5c330a')
text=text.replace('c03ea2f4aa30c5e32654c575115583f72815b9701c16791c4f94c6ade753f2ff','e301764df29325128695e087181091647648a20a6f7ad8c86589a12818e4496d')
text=text.replace("PREVIOUS/'recipes/build_candidate.py'","PREVIOUS/'recipes/build_java.py'")
text=text.replace("base='R76'","base='R77/R78'")
(ROOT/'recipes/build_java.py').write_text(text,'utf8',newline='\n')
spec=importlib.util.spec_from_file_location('compose_r79',ROOT/'recipes/build_java.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
_,_,sources,_=m.verified_sources()
value=dict(schemaVersion=1,base='R77/R78',sources={n:hashlib.sha256(p.read_bytes()).hexdigest() for n,p in sorted(sources.items())})
(ROOT/'SOURCE-MANIFEST.json').write_text(json.dumps(value,indent=2)+'\n','utf8',newline='\n')
print('Frozen',len(sources),'Java sources')
