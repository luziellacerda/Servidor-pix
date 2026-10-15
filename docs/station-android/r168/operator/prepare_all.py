"""Pure R168 preparation using explicit received identities and frozen reviews."""
from pathlib import Path
from copy import deepcopy
import importlib.util,json,sys
W=Path(__file__).resolve().parent
DOC=Path(__file__).resolve().parents[2]
def read(path):return json.loads(Path(path).read_bytes())
def load(name,path):
 spec=importlib.util.spec_from_file_location(name,path);mod=importlib.util.module_from_spec(spec);sys.modules[name]=mod;spec.loader.exec_module(mod);return mod
def prepare(catalog,profiles,modes,manifest,received,kit_review,prior_runtime,tools=None):
 root=Path(tools) if tools else DOC/'online-plataformas-20261009/library-tools'
 sys.path.insert(0,str(root));sys.path.insert(0,str(DOC/'r128/operator'))
 operator=load('r168_profiles',root/'station_online_profiles.py')
 builder=load('r168_builder',DOC/'r128/operator/build_incoming_manifest.py')
 merger=load('r168_merger',DOC/'r128/operator/merge_scoped_modes.py')
 compact=load('r168_compact_winback',DOC/'r168/operator/compact_winback.py')
 original_merge=merger.merge
 def merge_review(operator,catalog,profiles,modes,runtime,decisions=None,**options):
  return original_merge(operator,catalog,profiles,modes,runtime,
   compact.compact(decisions) if decisions is not None else None,**options)
 merger.merge=merge_review
 adapter=load('r168_received_policy',DOC/'r168/operator/received_policy.py')
 psx=load('r168_psx_decisions',DOC/'r168/operator/prepare_psx_bindings.py')
 old=load('r168_original_psx_evidence',DOC/'psx-online-20261015/prepare_psx_online.py')
 base=load('r168_reviewed_78',DOC/'r139/prepare_independent_owners.py')
 full=load('r168_complete_n64',DOC/'n64-completo-20261014/prepare_complete_n64.py')
 runtime=read(Path(received)/'engines-apk.json')
 adapter.configure(builder,merger,runtime,read(Path(received)/'incoming.json'),read(Path(received)/'profile-policies.json')['profiles'],
  prior_runtime)
 provenance=read(Path(received)/'references/reviewed-78-mode-provenance.json')
 assert provenance==read(DOC/'r133/catalog/reviewed-mode-provenance.json')
 research={name:read(DOC/'r133/catalog'/name) for name in {r['sourceFile'] for r in provenance['modes']}-{'profiles-complete.json','server-prepared-modes.json'}}
 first=base.prepare(catalog,profiles,modes,runtime,provenance,research,operator,builder,merger)
 incoming,_,decisions78,modes78,receipt78=first
 assert receipt78['addedScopedModes']==78
 profiles78=operator.prepare(catalog['items'],profiles,incoming,modes78)
 second=full.prepare(catalog,profiles78,modes78,runtime,read(DOC/'n64-completo-20261014/complete-review.json'),operator,builder,merger)
 assert second[-1]['addedScopedModes']==321 and incoming==second[0]
 modes399=second[3];profiles399=operator.prepare(catalog['items'],profiles78,incoming,modes399)
 merged=deepcopy(manifest);present={e['engineId'] for e in merged['engines']}
 assert not present & {e['engineId'] for e in incoming['engines']},'R168 already registered; reconcile the authority'
 merged['engines'].extend(deepcopy(incoming['engines']));operator.validate_manifest(merged)
 result,psx_decisions,psx_receipt=psx.prepare(operator,old,catalog,profiles399,modes399,merged,
  read(DOC/'psx-online-20261015/reviewed-library.json'),read(DOC/'psx-online-20261015/evidence/core-qualification.json')['editions'],kit_review)
 # The registrar projects once from the fresh baseline, in engine order.
 # Intermediate review projections must not reorder its final additions.
 final=operator.prepare(catalog['items'],profiles,incoming,result)
 assert final[:len(profiles)]==profiles and result['modes'][:len(modes['modes'])]==modes['modes']
 assert len(final)-len(profiles)==len(result['modes'])-len(modes['modes'])==492
 assert psx_receipt['approvedMultiplayerModes']==23 and psx_receipt['pendingMultiplayerModes']==17 and psx_receipt['localClassificationsWithoutSeats']==53
 # R167 remains immutable and each requalified decision must preserve its
 # actual active policy, in addition to rechecking the original HLE evidence.
 previous_psx={p['profileId']:p for p in profiles if p['engineId']=='pcsx-rearmed-ff81ed17-player-v3-4d5c4cb8f609'}
 assert len(previous_psx)==93
 for decision in psx_decisions:
  old_id=decision['profileId'].replace('psx-r168-20261015-','psx-r167-20261015-',1)
  previous=previous_psx[old_id]
  assert all(previous[k]==decision[k] for k in psx.FIELDS if k!='profileId')
  assert all(previous[k]==decision[k] for k in ('itemId','contentSha256','profileSha256'))
 summary=dict(passed=True,enginesAdded=4,profilesAdded=492,modesAdded=492,nativeReviewedModes=399,psx=psx_receipt,
  previousEngineCount=len(manifest['engines']),previousProfileCount=len(profiles),previousModeCount=len(modes['modes']),
  engineCount=len(merged['engines']),profileCount=len(final),modeCount=len(result['modes']),
  owners={e['platform']:e['engineId'] for e in incoming['engines']},allPreviousBindingsPreserved=True,physicalGameplayValidated=False)
 return incoming,result,final,dict(summary=summary,nativeReview78=decisions78,nativeReview321=second[2],psxReview=psx_decisions)
