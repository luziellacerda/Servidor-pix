# R73 — APP → SERVIDOR: cadastrar motor corrigido antes da instalação

Código completo do app: `5657dce678609f25501321e307839a6e0c018d4e`, branch `fix/station-r73-recovery-handshake-20261007`.
[Fontes, receitas, testes e estado](https://github.com/luziellacerda/TurboElden/tree/5657dce678609f25501321e307839a6e0c018d4e/versions/station-recovery-handshake-r73-20261007).
Base do servidor examinada: `c1e44a1225a101478ceb29f4e624885331872c0d`. Esta é uma entrega do cliente e um pedido ao operador, não um retorno do servidor nem uma implantação já executada.

**Ação: cadastrar as duas novas identidades exatas de SNES/Mega e runtime R73, preservando as seis existentes.** A leitura atual ocorre uma vez na inicialização do serviço; a ativação exige procedimento controlado pelo operador e coordenação das partidas. Não apagar/verificar menos os hashes.

Os dois aparelhos permanecem R72. R73 já está empacotada, mas aguarda cadastro e teste físico. APK b23ff3d1e319ee050e6eb867e2643a5f66661da481dd2e3a4e50089d1604f077; runtime9af2778898e4ba026d65d9b5c74ef3d8089e58bdbdf9be40f8e28f0eedcb14c2.

As evidências copiadas são do commit acima. Caminhos `evidence/`, `tests/` e `native/` abaixo correspondem a `entrega-app-r73-20261007/` nesta entrega; fontes completas/receitas estão no link do app. Não substituir Activities por snapshots antigos. A confirmação física de gameplay e recuperação continua pendente.

---

# APP → SERVIDOR: corrigir espera inicial da partida — R73

**Destinatário: operador do Station Android no Servidor-pix.** Pedido técnico do cliente, não resposta do servidor nem implantação já executada. O mantenedor relatou os dois aparelhos R72 na tela preta. Esta entrega produz o candidato corrigido e pede cadastro aditivo das identidades exatas abaixo antes do teste físico.

## 1. Fonte exata e estado

- App: branch `fix/station-r73-recovery-handshake-20261007`; commit completo no documento de entrega que acompanha este handoff.
- Pasta: `versions/station-recovery-handshake-r73-20261007/`.
- Base instalada nos dois: R72, APK `a8d3d28bb70618c52debf6e4cb0acaaf1f3bd1de19fe410dda85f6953caaec8e`.
- Servidor examinado: `c1e44a1225a101478ceb29f4e624885331872c0d`; produção declarada fonte `ab192bf1585e30f303d041f13b36a1f9c96d2caa`, DLL `815fc8bc99a9d16247488a1797d2726b928eb3e592fd8a19700371e8d57c3243`.
- R73 compilada e assinada: APK `b23ff3d1e319ee050e6eb867e2643a5f66661da481dd2e3a4e50089d1604f077`, 2122890150 bytes.
- Motor R73: `9af2778898e4ba026d65d9b5c74ef3d8089e58bdbdf9be40f8e28f0eedcb14c2`, 10668712 bytes, arm64/API26, alinhamento16KiB.
- DEX35: `d6ffa9060289e1afa1e39234c3bacedc8ead197fddcdc171b954bd580240be9e`. DEX28 preservado `1a04582a02804ecbe4b075dd487118a171b0b0f305dbb8884015bbede09b25d7`.
- Certificado preservado: `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`.
- **Não instalada e não homologada em gameplay.** Os dois aparelhos continuam R72. Não usar aprovação de outra versão como teste desta.

## 2. O que os aparelhos demonstraram

Nova tentativa em 07/10/2026 às22:48:19UTC, Samsung anfitrião e Motorola convidado. Ambos R72 exatos. Ambos registraram `native-hooks-loaded`. Host registrou `native-listening` e `host-listening-ack`. Os dois receberam STATE epoch1:0→1 e mantiveram PONGs. O mantenedor confirmou ambas as telas pretas; não foi observado state2/RUNNING.

O registro JNI está confirmado nos dois aparelhos R72; a causa física da tentativa anterior com R71 não foi demonstrada conclusivamente. Não atribuir a espera a queda de internet ou licença recusada. A hierarquia Android do host mostrou painel de espera visível, enquanto a captura estava preta: isso é compatível com encobrimento pela janela EGL, mas essa explicação visual ainda depende da conferência física da R73. Logs/imagens completos são privados; resumo saneado em `evidence/physical-r72-both-black.json`.

## 3. Defeito reproduzido e correção nativa

O pump de recuperação R71/R72 chama apenas `netplay_sync_pre_frame()` e retorna antes do caminho normal de post-frame. No handshake, o anfitrião aceita PLAY e enfileira MODE. Esse envio normalmente é descarregado em `netplay_post_frame()`, que não roda enquanto a recuperação espera os dois participantes prontos. O convidado depende de MODE para mudar para PLAYING; assim READY nunca libera a espera. PONG do WebSocket não descarrega o buffer nativo do RetroArch.

Corrigimos **somente `station_netplay_recovery_poll()`**. Depois de pre-frame, descarrega os buffers das conexões ativas com `netplay_send_flush(..., false)`, não bloqueante. Envio parcial/zero preserva bytes/offsets. Não confirma prontidão enquanto existir saída pendente. Falha real mantém o tratamento de falha existente. Não chama post-frame nem `retro_run` durante pausa, não avança quadros, não muda controles, core ou opções.

Fonte anterior `netplay_frontend.c` SHA `e221e40605aecce5f168fd68ab9f06fda1eb8297a97ba6c1aff3a6e660fa8d22`. Fonte nova `3dea62f27a197f2f4fa7c689a0ed09bbe8840ff1e00bf74edb299a2f40d40afa`. Função nova LF `d3a42419124dda592fea217088ed4e4687bf63718f657299ac8d73ad9fa41e12`. Patch exato em `native/recovery-handshake-r73.patch`, candidato em `tests/recovery_poll_candidate.h`. `tests/NATIVE-FINDINGS.md` detalha funções e linhas.

O teste extrai funções reais de buffer/envio/pump da fonte congelada, usando transporte e produtor de handshake controlados. Baseline falha após64 polls com MODE retido; candidato passa22 verificações, incluindo ordem/bytes, parcial, backpressure, ring wrap, erro real e prontidão. **Teste isolado, sem ROM, Android ou gameplay; não prova causa única no aparelho.** Os testes anteriores de transporte simulavam connected=true na conexão TCP e não cobriam esta etapa nativa.

## 4. Java e preservações

Dois arquivos sobre as198 fontes R72: Activity usa Dialog de janela própria para mostrar Aguardando conexão/Sincronizando sem disputar a janela EGL; não toma foco nativo, remove painel ao retomar/ocultar/destruir, preserva saída humana. Tunnel acrescenta somente diagnóstico numérico limitado a seis amostras por espera em até60s; não altera o protocolo ou desconexões.

1206 verificações executáveis Java,22 nativas e39 guardas de fonte/ciclo de vida passaram. DEX28 idêntico. No APK mudaram exatamente DEX35, biblioteca compartilhada de netplay e manifesto de motores; 13222 entradas preservadas. Núcleos SNES/Mega, opções, controles, BIOS,58 vídeos, design,30fps de menu, licença e saves preservados. NeoGeo continua `launchReady=false`; não foi liberado por esta correção. Campos de engineId/hash dele refletem o ELF compartilhado, sem solicitação de habilitação.

## 5. Ação exata solicitada ao operador

**Adicionar as duas entradas de `evidence/server-engine-registry-additions.json` ao registro efetivamente carregado pelo Station.** Antes, conferir o registro ativo e seu mecanismo de reload com base no código/serviço real. Preservar todas as seis entradas anteriores; não sobrescrever IDs antigos com o novo hash. Runtime comum às duas novas entradas: `9af2778898e4ba026d65d9b5c74ef3d8089e58bdbdf9be40f8e28f0eedcb14c2`; protocolo `station-stream.v2`.

| Plataforma | ID novo | Core SHA-256 preservado |
|---|---|---|
| snes | `bsnes-mercury-performance-79d7f9de-rs3-9af2778898e4` | `cc14438319709f3b7868b3e652c731f6df472ff21bb01e4a65234ba2cc6f368b` |
| megadrive | `clownmdemu-d43c2708-rs3-9af2778898e4` | `109b62ac11b2f59572de0666bcb02b3701676896b91cb32529bbe5c5032b6b69` |

Não mudar contrato TSR2, relay-wss-v2, assinatura de envelopes, licenças, RequireVerifiedApp, catálogo, outros produtos ou bancos. Não são necessárias novas credenciais, alterações de core ou troca por ELF compilado no Linux: registrar o hash exato do ELF Windows incluído neste APK.

Ler `StationOnlineGame.prepare/verifyRoom` e `StationOnline.cs`: ambos validam IDs/hashes. O cliente novo continuará recusado até o registro assinado conter a combinação exata. Não desativar essas verificações para testar. R72 e R73 não compartilham a mesma identidade de motor: usar os dois aparelhos R73 e sala nova, depois do cadastro.

### Ativação efetiva e partidas existentes

No código examinado, `StationOnlineRegistration.cs` lê `EngineRegistryFile` uma vez em `AddStationOnline` e captura o registro no serviço singleton. Não foi identificado hot reload nesse caminho. Apenas editar o JSON não ativa os novos IDs. O operador deve planejar a reinicialização controlada da instância Station, coordenando antes a saída das partidas existentes, ou apresentar evidência de outro mecanismo realmente implementado. As salas e os streams de recuperação ficam em memória: uma reinicialização encerra esse estado. Este handoff não autoriza interromper partidas por conta própria.

## 6. Retorno pedido (sem suposições)

1. Identificar commit/caminho exato do registro alterado, serviço/DLL/PID ativos, hash do registro e momento efetivo do reload. Distinguir código no Git de produção.
2. Demonstrar lista assinada `engines` contendo as duas adições, com os seis antigos preservados e recuperação v2 ativa.
3. Informar se houve alguma alteração de contrato/estado além do cadastro; caso positivo, fornecer código e evidência antes de alegar compatibilidade.
4. Correlacionar próxima tentativa R73: tickets host/guest, host-listening, PAUSED, bytes/offsets/READY de ambos e transição1→2. Não publicar tickets, tokens, licenças ou logs pessoais.
5. Confirmar procedimento de retorno que preserve clientes anteriores; não remover o registro antigo durante a homologação.

Depois do retorno, instalar por transferência direta, mesma assinatura, sem limpar dados nem interromper partida. Testar ambos os papéis, nomes, primeira imagem/áudio, comandos dos dois jogadores, saída para plataformas. Testar interrupção/retomada de rede separadamente. Registrar falha remanescente com `event=wait-diagnostic` (epoch/state/nativeStatus/offsets), sem inventar sucesso por PONG.

## 7. Reprodução e artefatos locais

- Nativo: `E:\R73fixed`; receita `recipes/build_native_r73.py`, fonte oficial congelada + patches R71 verificados + função R73 testada. Source ZIP e NDK são os mesmos da R71; recibo documenta hashes/comando.
- Java/temp: `E:\ESTUDO APK\work\station-recovery-handshake-r73-20261007-build`; receitas build_candidate.py/run_tests.py.
- APK final: `G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Premium-R73-20261007.apk`.
- Manifesto/adições: recipes/prepare_engine_registry.py; empacotamento recipes/package_r73.py. Assinatura local privada original, verificada antes de assinar.
- `SOURCE-FILES.json` lista todas as fontes/recibos e hashes; recibos native-build/java-dex-build/local-tests/package ligam código ao candidato.

Nenhum deploy Linux foi executado por esta entrega. Nenhum APK, ROM, BIOS, segredo ou captura privada publicado. Não marcar estável ou prometer fim de toda tela preta antes do teste físico.
