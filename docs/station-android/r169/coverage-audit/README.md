# Auditoria somente leitura da cobertura online R169

Este auditor cruza **cada edição** do export autenticado do catálogo atual com os motores online declarados no kit Android R169, os motores registrados no servidor e os vínculos exatos de perfis e modos. Ele gera uma linha por `itemId`, com plataforma, formato, motor elegível, modo local ou multiplayer, evidência, motivo da falta de vínculo e totais por plataforma. **Ele não cadastra motores, não aprova modos, não chama APIs e não altera os quatro exports de entrada.**

## Executar no servidor

Use os mesmos quatro exports JSON **atuais** e protegidos usados pelo operador R169 em `../operator/prepare_all.py`: catálogo (`--catalog`), perfis (`--profiles`), modos (`--modes`) e manifesto de motores (`--manifest`, aqui `--server-engines`). Os caminhos concretos são privados e devem ser obtidos da execução protegida do operador. Não substitua o catálogo atual pelo snapshot versionado de revisão 44.

```sh
python3 -B docs/station-android/r169/coverage-audit/audit_catalog.py \
  --catalog "$CATALOG_JSON" \
  --profiles "$PROFILES_JSON" \
  --modes "$MODES_JSON" \
  --server-engines "$ENGINE_MANIFEST_JSON" \
  --installed-kit "$R169_KIT_ZIP" \
  --policy-module docs/station-android/online-plataformas-20261009/library-tools/station_online_profiles.py \
  --review-ledger docs/station-android/r167/psx-reviewed-decisions.json \
  --review-ledger docs/station-android/n64-completo-20261014/complete-review.json \
  --expected-items 4744 --expected-revision 100 \
  --output-dir "/mnt/DADOS/station-r169-coverage-audit-$(date -u +%Y%m%dT%H%M%SZ)"
```

O ZIP é o kit privado R169 já conferido pelo operador. O script lê **somente** seu `engines-apk.json`; não publica o ZIP e não copia o APK. O destino deve ser uma pasta **nova** em volume protegido. O auditor recusa revisão ou número de itens inesperado, `itemId` duplicado, manifesto inválido, arquivo ausente e destino existente. Não inclua os quatro exports nem as saídas por edição no Git, pois contêm dados privados do catálogo. Para o handoff, publique somente totais agregados sanitizados e a avaliação das lacunas.

Arquivos produzidos:

| Arquivo | Conteúdo |
|---|---|
| `coverage-summary.json` | Revisão, 4.744 itens esperados, totais por plataforma e classe, hash das entradas, sem mutação do servidor. |
| `edition-status.jsonl` | Uma linha por edição: `itemId`, plataforma, extensão, motor elegível, modos e razões exatas. |
| `actionable-gaps.tsv` | Edições que exigem ação ou verificação; mantém razões legíveis para o operador. |

## Como interpretar

O auditor normaliza `nes` e `nintendinho` para a mesma plataforma. A ausência de motor **no manifesto instalado R169** gera `NO_INSTALLED_ONLINE_ENGINE`, mesmo que exista um dos 91 cadastros históricos no servidor. Uma extensão não declarada pelo motor instalado gera `FORMATO_INCOMPATIVEL`. Identidade ou capacidade divergente no servidor gera `SERVER_ENGINE_UNAVAILABLE`. Perfil que não bate com `(itemId, contentSha256, engineId, coreSha256, runtimeSha256, profileSha256)` gera `NO_EXACT_PROFILE` e seu motivo específico. Esses estados não podem ser resolvidos só aprovando um modo.

Com o motor e perfil exatos presentes, o relatório separa `LOCAL_ONLY`, `MULTIPLAYER_REVIEW_PENDING`, `REGISTRY_APPROVED_WITHOUT_GAME_PROOF` e `REVIEWED_MULTIPLAYER_APPROVED`. Cada modo aprovado também possui `documentedExactMode`, `reviewedGameModeEvidence` e `physicalAndroidGameplayValidated` independentes. Quando uma edição contém modos com evidências diferentes, a razão `OTHER_APPROVED_MODES_LACK_GAME_PROOF` impede que o resumo oculte o modo ainda não comprovado.

O registrador histórico em `station_online_profiles.py` pode atribuir `approved=true` e 2 jogadores a jogos não arcade quando `metadata.players != "1"`. Isso **não prova** que o modo funciona online. O auditor só marca revisão por jogo quando cruza escopo exato do modo e fonte com uma decisão imutável por edição, hash, modo, contagem e controle. O ledger versionado pode explicar uma pendência; não concede aprovação nova. A marca de teste físico Android é separada da aprovação documental. HTTP 200, QEMU, sala criada ou sala `playing` não comprovam imagem, áudio, controles independentes nem saída correta.

## Limites já medidos antes do export atual

O recibo aplicado R168 informa catálogo revisão 100 com **4.744 itens**, 91 motores históricos, 16.896 perfis históricos e 11.981 modos históricos. São registros acumulados, não cobertura online por edição. PSX tinha 84 edições e 93 decisões: 23 modos 2P aprovados em 19 edições, 17 modos 2P pendentes em 12 edições e 53 edições locais. N64 tinha 157 edições; o usuário confirmou jogo físico N64 funcionando, portanto esta auditoria **não altera sua lógica**. GameCube/Wii ainda precisam de classificação por edição, sem aprovação por título ou metadado.

O snapshot versionado de revisão 44 contém **4.690**, não 4.744, itens. Cruzado com o manifesto online do APK R169, ele mostra **1.087 edições sem motor online no cliente** (NES/Nintendinho, Dreamcast, PS2, PSP, Saturn, Sega 32X, Switch e Wii U) e **seis edições com extensão não declarada** (`.smd`, `.fig`, `.swc`). Esses são números históricos de triagem: a contagem final precisa vir do comando acima contra o export rev100. A classificação `available` nesse snapshot diz respeito à disponibilidade de catálogo; não é prova de multiplayer por jogo.

Capcom vs. SNK — Millennium Fight 2000 Pro, `station_767e5e92f2f353479e6120f1c28f96a7`, já possuía VS 2P aprovado na R168. Se a R169 não o admite, conferir o vínculo **exato** da identidade R169 e a publicação aplicada no servidor antes de alterar o jogo. Não aprovar em massa os 17 modos PSX pendentes; há divergência de replay de RAM e outras faltas de prova registradas por edição.
