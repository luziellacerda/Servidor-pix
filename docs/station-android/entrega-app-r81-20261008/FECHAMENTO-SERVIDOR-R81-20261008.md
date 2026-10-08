# Fechamento único — ativar salas para o cliente R81

## Decisão e estado comprovado

O mantenedor solicitou concluir a integração com código pronto e uma entrega objetiva. **R81 está compilada e assinada**, SHA256 `85fac8f51daa3a370eabb14d98832f75c2f30ff81422c549538ee715627032c6`, 2.123.655.964 bytes. Não foi instalada nesta rodada. Corrige rejeição de classificação individual que bloqueava outros modos e mensagens indevidas de ativação. Fonte Java completa e testes acompanham a entrega. Moto R79/Samsung R78 são os últimos recibos de instalação; não presumir atualização.

Esta lista consolida as pendências anteriores. A conclusão depende de **implantação verificada e teste de partida**, não de outro recibo de leitura. Retorno examinado: `a4fd0d73a7eaaf43fafbae1c42e9b83580985433`. Candidata funcional: `b472d8a653e065cccb00dfb15dbea3d56c03db98`; DLL candidata `9878ae9caea55bb6834745caa3a60140616d3e0a5f2ea71055fe9e9df813fe51`. Produção declarada ainda `ab192bf`/PID1278094/v2/dez engines; zero perfis reais aprovados. Sinopses revisão19 já aplicadas.

## Execução pelo operador — um único ciclo

1. **Preparar identidades reais persistentes.** Executar `server-tools/prepare_content_identity_registry.py` entregue aqui com export atualizado e mapa privado dos artefatos efetivos. Para preservar identidades existentes, fornecer o registro atual em `--existing`; gerar um arquivo novo em `--output`. A ferramenta verifica tamanho/SHA do ZIP ou raw e calcula o SHA do arquivo de lançamento exato, sem extrair ou normalizar ROM. Configurar `contentIdentityRegistry` no importador candidato. Executar `docs/station-android/scripts/atualizar-biblioteca-station.py --config <configuração efetiva>` e usar esse mesmo importador nas varreduras agendadas. O JSON deve incluir todas as identidades qualificadas necessárias, pois omissões retiram o campo do índice. Confirmar persistência após outra varredura.
2. **Preparar os perfis exatos.** Vincular itemId/conteúdo/plataforma/engine/core/runtime/controller/hash de perfil/modo/capacidades e qualificar os controles. Pilotos: Battletoads in Battlemaniacs para duas pessoas; Super Bomberman 2 USA, somente Battle → Single Match, com todos os pads MAN, para `[2,3,4]`. O modo Normal individual não ganha vagas. O piloto Bomberman recebido ainda tem `approved:false`; não mudar esse valor por teste sintético. Os detalhes e IDs sem ambiguidade estão em `server-tools/README.md`.
3. **Preservar a admissão R76.** Conservar as dez engines v2 em `Station__Online__EngineRegistryFile`. Preparar exatamente um perfil aprovado `standard-2p-v1` permitindo2 por chave real legado item/conteúdo/engine/core/runtime. Duplicar perfis nessa chave também recusa a admissão. Ativar globalmente apenas os dois pilotos bloqueia os demais títulos legados; não tratar isso como migração concluída. IDs v3 pertencem ao registro separado de perfis, não ao registro v2.
4. **Qualificar e publicar somente Station.** Usar a candidata conciliada, resolver/documentar a divergência TLS v2 sem logging ainda pendente, validar versão antiga/nova e reversão antes da troca. Confirmar ausência de partidas e sessões de recuperação retidas; o estado das salas está em RAM. Configurar `Station__Online__MultiplayerEnabled=true`, `Station__Online__MultiplayerLegacyCapacityGate=true`, `Station__Online__MultiplayerProfileRegistryFile=<arquivo absoluto>` e manter Recovery/Online/Relay habilitados. A leitura dos perfis ocorre na inicialização; editar JSON isoladamente não ativa o processo. Os scripts R74 têm baselines antigos e não devem ser executados como implantação R81.
5. **Comprovar pela rota pública e executar partida.** Catálogo autenticado/assinado deve trazer contentSha256 correto, inclusive após nova varredura. `POST /v1/station/online/multiplayer/command`, ação capabilities com itemId, deve devolver envelope válido `multiplayerVersion:3`/`station-multiplayer.v3` e o perfil aprovado exato. Criar sala nova, confirmar participantes/Pronto e iniciar pela R81 igual nos aparelhos. Exercitar Battletoads2 e Bomberman2 com2/3/4, controles exclusivos, pausa/retomada e saída. Confirmar também novas salas do canal R76. Sem quatro aparelhos, a homologação4p fica explicitamente pendente.

## Identidades do cliente R81 — sem cadastro novo

A pasta `activation/` contém os manifestos de motores lidos dos APKs R76/R81 e conferidos pelo SHA integral, os hashes canônicos de controladores/opções para ambos os canais e os três registros exatos dos pilotos na revisão19 (um ID legado Battletoads é oculto de compatibilidade). São dados públicos de integração; não incluem ROMs nem autorização de capacidade. Usar o descritor atual do servidor para confirmar o export antes do vínculo. Não será necessário pedir novamente esses campos ao app.

| Plataforma | Engine | Core SHA256 |
|---|---|---|
| SNES | `bsnes-mercury-performance-79d7f9de-mt1-mp3-351cee4540e9` | `0a3ac7b4fa5da318b1c993d8867aa944eaa2d012680b49e9929bf7a4a398f527` |
| Mega Drive | `clownmdemu-d43c2708-mp3-351cee4540e9` | `109b62ac11b2f59572de0666bcb02b3701676896b91cb32529bbe5c5032b6b69` |

Runtime comum `351cee4540e916468a504788911e4c5fcb4fe141e3b544b1b1255d32e1d04a26`; protocolo `station-stream.v3`. Hash do perfil SNES Multitap `f59408b49f6e110a569fe9498af433b9f67c9586cbb8648238b8e571bb8b0143`. NeoGeo continua launchReady=false; não liberar por analogia. R81 preserva binários e manifestos R79/R77 integralmente.

## Retorno suficiente para encerrar

Enviar uma evidência consolidada contendo commit/DLL/PID/horário ativos; hashes dos registros e contagem de perfis aprovados; resultado sanitizado de catálogo/capabilities assinados dos pilotos; continuidade R76; resultados efetivos dos testes de partida ou o primeiro bloqueio concreto com horário/código. Não enviar sessões, tokens, licenças, ROMs, BIOS ou logs pessoais. HTTP200/PONG isolado e testes loopback não comprovam gameplay.

Convites/códigos v3 continuam ausentes na candidata recebida e no fluxo do cliente. A admissão disponível é pela lista de salas e conversa; não anunciar migração completa desses recursos. Os testes locais R81 não alteram esse fato nem demonstram estabilidade WAN. Nenhuma implantação Linux foi executada pelo PC.
