# R77 — salas de 2 a 4 jogadores e informação por edição

## Estado desta entrega

Candidata de implementação, sobre a R76 instalada. Não é atualização de produção nem homologação de partidas em três/quatro aparelhos. O APK final, seus hashes e as verificações finais ficam nos recibos desta versão. Nenhum Linux, serviço ou telefone foi alterado nesta etapa.

A quantidade de jogadores não é presumida pela plataforma, pelo nome ou por um campo descritivo antigo. A abertura da sala exige um perfil aprovado e assinado do jogo exato, modo, conteúdo, core, runtime e configuração de controles. O registro de produção continua sob responsabilidade do operador. Os perfis entregues para revisão permanecem `approved: false`.

## O que mudou

- Salas com capacidades explícitas de 2, 3 ou 4 pessoas, somente quando o modo permite aquela quantidade; conjuntos como 2 ou 4 não autorizam 3.
- Escolha do modo antes das vagas quando houver mais de um perfil aprovado.
- Posições P1–P4, nomes e confirmação de cada participante; número de vagas separado do limite do jogo.
- Cabeçalho compacto com as quantidades confirmadas. Criar sala, entrar e iniciar mostram modo, controles, ocupação e instruções. Mudança de geração/perfil/roster invalida a confirmação anterior.
- Contador do carrossel ligado a evidência por item, revisão e conteúdo. Ausência de confirmação aparece como `—`; foram retiradas estimativas por nome/plataforma. As estrelas e a composição visual do carrossel foram preservadas.
- Um canal ordenado e recuperável entre anfitrião e cada convidado. Três canais para uma sala de quatro, com posição de controle vinculada à conexão.
- Pausa coletiva quando qualquer participante perde transporte; retomada somente depois de todos os canais confirmarem a mesma época, esvaziarem as filas e sincronizarem o motor.
- Correção do mapeamento de controles adicionais no Multitap do SNES. Os emuladores, controles e saves offline permanecem preservados.

## Recibo final da candidata

APK compilado e assinado: **`14450f3aa52ca2c795b50afba7e5a75c5bd4f71aa0007d2c43c6542051bdb737`**, 2,122,993,964 bytes. Local: `G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Premium-R77-20261008.apk`. Certificado original preservado. Não instalada; nenhum servidor Linux alterado.

Java: 209 fontes (24 novas/alteradas, 185 preservadas). DEX28 `846f7f8556c42816a9465bf1338f8ebf0a29a57162b04e8e5645e1fc9b5c330a`; DEX35 `e301764df29325128695e087181091647648a20a6f7ad8c86589a12818e4496d`. Foram alteradas exatamente seis entradas do APK e acrescentado apenas `assets/station-catalog/player-evidence-v1.json`; 13,219 entradas e os 59 vídeos permaneceram idênticos. O asset de contagens revisadas permanece vazio: a implementação está pronta para receber vínculos comprovados, mas a pesquisa não foi promovida em massa a fato.

| Verificação local | Resultado |
|---|---:|
| Contrato/perfis/coordenador Java | 1.612 |
| Concorrência do túnel | 148 |
| Combinação de snapshots | 90 |
| Saída humana vinculada à sala/geração | 227 |
| Apresentação de jogadores | 188 |
| Métodos de interface extraídos | 87 |
| Evidência factual do carrossel | 175, mais 4.000 publicações concorrentes |
| Runtime nativo | 758 e 12 guardas |
| Mapeamento Multitap SNES | 6.609 |
| Servidor C#/HTTPS-WSS/v2 | 253 + 152 + 91 |
| Java ↔ C# HTTPS/WSS, 2/3/4 participantes | 9 sessões, 351 verificações, 25.560.000 bytes exatos |
| Inventário / metadados pesquisados | 12.361 / 73.525 |

A integração usa rede TLS local real e prontidão nativa simulada. Não executa jogos em Android nem mede latência de Internet. Consulte `tests/README.md`, `evidence/package.json`, `evidence/package-gates.json` e `STATUS.json`.

## Pesquisa do catálogo

O inventário percorre os 2.467 IDs do último catálogo publicado examinado, dos quais 2.212 são visíveis. Isso **não significa 2.467 fichas factuais homologadas**. A pesquisa separa correspondência de edição, evidência documental e conteúdo realmente verificado.

Consulte `catalog/METADATA-RESEARCH.md`, `metadata-coverage.json`, `catalog-game-metadata.json`, `mode-evidence.json` e `proposed-registry.json`. Há 1.454 correspondências exatas de edição/tamanho nas bases pesquisadas, 734 sem correspondência e 279 divergências de tamanho/container. Máximo descritivo não prova simultaneidade e não autoriza multiplayer. Sinopses e avaliações ausentes continuam pendentes; não foram criadas avaliações nem copiados títulos como sinopses.

Super Bomberman 2 é o piloto documental: Batalha aceita até quatro controles; Normal é individual. O perfil proposto identifica o item/raw SHA do catálogo publicado, mas depende da conferência atual do operador e da qualificação do conteúdo e dos controles. Battletoads tem discrepância entre rótulo USA e arquivo interno ESP/NTSC; não foi liberado por semelhança de nome.

## Reprodução e rastreio

- `recipes/implement_java.py`, `refine_java.py`, `refine_information_ui.py`: alterações sobre as fontes R76 verificadas, sem restaurar Activities antigas.
- `recipes/refine_catalog_java.py`: ponte estreita do catálogo para evidência de jogadores.
- `recipes/build_java.py`: inventário completo, JDK/D8 fixos, DEX 28/35 e checagem de deriva de fontes.
- `native/`: runtime R77, referência reproduzida e 20 bytes de build-id divergentes documentados; não adulterados.
- `core-snes/`: baseline byte a byte e delta de Multitap.
- `carousel/`: reprodução byte a byte R75 antes de trocar exclusivamente a origem do contador.
- `server/`: candidata C# sobre a DLL em produção declarada `ab192bf`; não inclui automaticamente a candidata 6f27 que o operador ainda não qualificou.
- `recipes/package_candidate.py`: base R76 exata, assinatura idêntica, lista fechada de entradas e comparação integral do APK.


- `recipes/refine_presence_java.py` deve executar depois de `refine_information_ui.py`: registra a intenção humana de saída com sala, geração e protocolo, conserva falhas de rede e não sai de outra sala.
- Ordem final das receitas de overlays: `implement_java.py` → `refine_java.py` → `refine_information_ui.py` → `refine_catalog_java.py` → `refine_presence_java.py`; então `freeze_sources.py`, `build_java.py`, `prepare_package_inputs.py` e `package_candidate.py`. Não regenerar fontes depois do freeze sem nova revisão/compilação.

## Limites

Os testes locais, incluindo TLS real e canais múltiplos, não substituem gameplay em Android. Não foi instalada esta candidata. A R76 instalada continua sendo a referência de uso. A nova aprovação por jogo precisa ser populada com evidências e hashes conferidos; a pesquisa descritiva não é promovida automaticamente. Os convites sociais/códigos v2 não são reutilizados em salas v3: neste candidato, a entrada v3 ocorre pela lista de salas e as conversas existentes continuam disponíveis. Essa limitação precisa ser resolvida/aceita na qualificação antes da substituição da versão em uso.
