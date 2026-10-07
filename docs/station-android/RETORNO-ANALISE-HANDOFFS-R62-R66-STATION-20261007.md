# Retorno da análise dos handoffs R62 a R66 — Station

07/10/2026. **Leitura e comparação; sem nova implantação.** Foram lidos os seis documentos APP → SERVIDOR da revisão `7a5db18564d50607a6ef90ef31b45a03232ae45a`, os recibos e a fonte Android até `7d0d3da5c979b8ed3c7fb235cd6eddb0dcba2e65`. Implementação R66: `291f3949760c0d77030e4872520efc3e1c8f928b`.

[Recibo da análise e inventário dos 189 cartuchos](retornos-r62-r66-20261007/ANALISE-LEITURA-20261007.json). Os seis documentos recebidos foram importados preservando exatamente seus blobs Git e fins de linha.

## Estado conciliado

| Entrega | Evidência atual | Consequência |
|---|---|---|
| R62 | Prontidão TCP/WSS recebida incorporada; APK real, criação confirmada, capas e miniaturas de salas | Preservar essas alterações ao integrar outro delta |
| R63 | APK `d9a35602…` instalado e hash integral conferido no Samsung A56 e Motorola Edge30 | Convite curto e senha automática já integrados; instalação desses recursos deixou de ser pendência |
| R64 | Controles e diagnóstico de queda preparados; herdados pela R65/R66 | Não implementa retomada de conexão |
| R65 | Lançador Neo Geo CD incorporado em DEX30; instalação posterior no Samsung registrada | A integração não ficou apenas no Git; o defeito subsequente de BIOS foi tratado na R66 |
| R66 | APK `e4397fd7…` instalado no Samsung, identidade/dados preservados; catálogo confirmado após desbloqueio | Usar R66 como base da próxima alteração; execução CD não foi capturada |
| Segurança do servidor | API `da07355`, PID1147382, usuário próprio, isolamento publicado | Continua em produção; atualização protegida do APK precisa de conciliação com R66 |

O último APK conferido no Motorola continua R63. Não existe recibo novo do POCO nesta série. O mantenedor relatou que os dois aparelhos jogaram e os comandos responderam antes da queda. Isso é evidência humana recebida; a reprodução instrumentada em dois Android continua pendente.

Os README/STATUS de preparação R65/R66 ainda contêm “não instalada”. Os recibos posteriores e o bloco atual do AGENTS identificam as instalações efetuadas. Eles têm precedência para o estado do aparelho, mantendo o histórico de preparação intacto.

## Servidor observado nesta leitura

O ExecStart atual aponta para `/opt/turborama-station-security-20261007-da07355/TurboRamaSuiteOnlineServer.dll`, com PID1147382 e usuário `turborama-station-api`. O hash publicado permanece documentado no [retorno de segurança](RETORNO-SEGURANCA-STATION-20261007.md); o binário protegido não foi relido por esta conta.

API, management, helper de emissão e timer de importação estão ativos. A saúde e as duas prontidões locais retornaram JSON/HTTP200; relay sem conexões ativas na consulta. O catálogo público sem credencial retornou JSON/HTTP401, como esperado.

**Atenção à verificação de saúde:** `https://app.lzgames.com.br/health` e os caminhos públicos `/ready/...` retornam HTML do Portal do Consumidor/HTTP200. Esse status não comprova saúde da API Station. Usar a prontidão local e respostas reais das rotas `/v1/station/`; manter o portal compartilhado funcionando.

A última prova autenticada do catálogo disponível nesta leitura é revisão14/2212 visíveis/2467 totais, do recibo de publicação. O índice ativo está protegido; não houve nova exportação autenticada nesta análise. O TSV histórico usado no pedido foi conferido por SHA256 `3dae0cfa3877fba31b9a347b58795fe0df95333aea628b9ea8be597b7ccfed6e`.

## Queda online: correlação Q01 a Q08

Pedido: [queda e retomada](PEDIDO-QUEDA-RETOMADA-PARTIDA-STATION-20261007.md). Fonte de diagnóstico R64: `8812bacf8154f25fd7739f95a2db0729f36e4c72`, herdada pela R66.

Foram lidos 648 registros da API entre12h20 e12h26UTC. Nenhum identificador de conexão, credencial ou log bruto foi publicado.

| UTC em07/10 | Registro |
|---|---|
| 12:21:45.330 | Primeiro GET do relay iniciado |
| 12:21:46.341 | Segundo GET do relay iniciado |
| 12:23:46.378 | Renovação de sessão HTTP200, duração12,7359ms |
| 12:24:07.346–.347 | Aplicação abortou as conexões; ambos os GETs terminaram com upgrade101, após142,017s e141,006s |
| 12:24:07.422 | App do anfitrião registrou relay-failed/RELAY |

Na janela,34 comandos online terminaram emHTTP200; três renovações de sessão também200. Não apareceu401/403/500 nesse recorte. Dois polls de eventos terminaram499 antes da queda. Os corpos não foram registrados: **não é possível identificar todos esses comandos como heartbeats nem atribuir o último heartbeat a cada participante**.

A queda ocorreu antes da publicação de segurança das15h38UTC. A cronologia não sustenta atribuí-la à nova proteção. O primeiro gatilho permanece desconhecido: faltam código de fechamento/exceção inicial, lado e última presença individual. “A aplicação abortou” demonstra o encerramento conjunto, sem identificar o que levou ao primeiro finally.

O código atual conserva o mecanismo descrito no pedido:

- `StationRelay.Attach` termina ou falha e executa `pair.Stop.Cancel`, Abort nos dois lados e `CloseRelay`.
- `CloseRelay` chama Leave; a sala ativa pode desaparecer.
- `Sweep` remove presença após60s; Watch verifica a lease a cada2s.
- Ticket é de uso único e `usedRelayPeers` impede reanexação na geração atual.
- Estado de sala e transporte são mantidos em memória; não há protocolo de recuperação persistida.

Os snippets atuais do proxy mantêm120s de inatividade de IO no WSS. Os142s observados não comprovam que o proxy iniciou a queda. Os55 avisos JNI StackOverflowError recebidos do Android também precisam de correlação; o recorte não contém stacktrace que permita declará-los causa.

**Q01–Q03:** correlação acima acrescenta evidência do Linux; primeira causa e heartbeat individual continuam abertos. **Q04:** atrasos com motores reais não foram simulados nesta leitura. **Q05–Q08:** retomada ainda requer contrato e implementação conjunta, não estão publicadas.

### Implementação necessária para a retomada

Conservar v1 para clientes atuais e acrescentar transporte negociado. A sessão lógica e os membros devem sobreviver à perda do socket; liberar o slot físico e separar presença social da partida em espera. Pausar realmente os dois motores, conservar inputs/estado, emitir nova credencial individual autenticada e confirmar sincronização antes de retirar “Aguardando conexão…”.

A ponte precisa de sequência/offset, confirmação, replay limitado, deduplicação e backpressure, ou de reinício sincronizado com estado aceito pelos dois. Reconectar um WSS e reenviar bytes sem esse contrato não comprova retomada.

Mudanças no runtime exigem novos hashes/engineIds e registro aditivo; conservar controles, Binder, saída humana e autenticação. Definir recuperação após reinício separadamente: a memória atual não sobrevive ao serviço/aparelho desligado. Validar a matriz de perdas, troca de rede, esperas acima60/120s, renovação, revogação e saída humana com os dois aparelhos. Estes pontos são requisitos; nenhuma implementação v2 é alegada neste retorno.

## Neo Geo e CD: NG-01 a NG-07

Pedidos: [auditoria R65](PEDIDO-AUDITORIA-NEOGEO-CARTUCHO-CD-R65-20261007.md) e [correção da análise de BIOS R66](ADENDO-BIOS-CD-JA-INCLUIDA-APP-R66-20261007.md).

Foi lida a tabela central dos **189 ZIPs originais** correspondentes ao relatório recebido, incluindo os13 nomes não registrados e os26 itens da coleção KOF. O recibo contém nomes, tamanhos, CRC32 declarado e correspondência com IDs/revisões/capas/hashes do TSV histórico.

Essa leitura não descomprime/verifica os bytes de cada chip e não prova igualdade com o artefato hoje servido. Não representa uma verificação adicional na rotina de download.

| Pedido | Resultado desta análise | Trabalho que permanece |
|---|---|---|
| NG-01 | Fonte/PID atuais e revisão histórica conciliados;189 correspondências documentais | Export autenticado do índice/catalogo ativo |
| NG-02 | Inventário dos13 nomes disponível; ausência de registro segundo a auditoria MAME0289 recebida | SHA/CRC reais, driver exato e compatibilidade; alias somente após prova |
| NG-03 | Original `kof98.zip` legível:53 membros,16 chips do jogo mais dependências/BIOS identificadas | Comparar bytes, BIOS escolhida e pacote instalado/servido; causa gráfica não comprovada |
| NG-04 | Inventário individual de todos189 originais, com central directory legível | Conferir membros contra drivers e pacote autocontido no rompath real |
| NG-05 | R66 usa automaticamente firmware já existente nos assets;50 CHDs no documento recebido | Verificação completa dos CHDs ativos/parentes e execução no Android |
| NG-06 | `pspikes` e `spy` classificados como outro hardware no relatório recebido | Plano de classificação preservando IDs, capas, instalações e saves |
| NG-07 | Relatório e inventário publicados para continuidade | Não houve correção de pacote/revisão de conteúdo nesta leitura |

No ZIP original de KOF98 há `242-p1.p1`, `242-p2.sp2`, oito chips C, quatro V, M1 e S1, além de `000-lo.lo`, `sfix.sfix` e múltiplas BIOS. Uma falta genérica de BIOS nesse original não foi demonstrada. Integridade, conjunto exigido pelo MAME e igualdade com a instalação continuam por verificar.

A R66 corrige uma omissão do cliente: o lançador novo ignorava os assets de firmware que o APK já tinha. Preservar `MameEntryActivity` e `NeoCdSupport` da R66. Não acrescentar BIOS ao download para solucionar esse defeito. Não renomear os13 conjuntos por tentativa nem atribuir a imagem de KOF98 a ROM/BIOS/GPU sem a comparação.

## Segurança Android: base antiga precisa ser conciliada

A proteção do servidor está publicada. O delta Android `213cfce` foi preparado sobre R57, enquanto a produção Android evoluiu por R62→R63→R64→R65→R66.

O empacotador antigo exige APK `e6159fa3…` da R57. Ele não deve orientar a atualização do APK R66. Trocar apenas o hash esperado também não garante conservação da composição.

Próxima entrega precisa:

1. Compor R55→R57→R62→R63→R64 e preservar o DEX30 R66/recursos existentes.
2. Conciliar semanticamente prova e diagnóstico nos quatro arquivos online: `StationOnlineClient`, `StationRetroLaunch`, `StationRetroActivity` e `StationRelayTunnel`; preservar também `StationGameSession` R64.
3. Integrar os cinco arquivos do cliente de segurança, recompilando DEX28 e35 juntos com o novo client.jar.
4. Empacotar sobre o **APK R66/e4397fd7…**, conservando DEX30, controles, capas/salas R62, runtime899e3527, assinatura original, alias RSA e dados. Exigir novos manifestos/recibos de preservação.
5. Atualizar e qualificar ambos os aparelhos antes de exigir atestação global.

`RequireVerifiedApp=false` preserva os clientes atuais. R63/R66 recebidas não incorporam a prova por pedido; acesso exclusivo ao APK original não está demonstrado. A prova do backend já funciona para vínculos que aderiram. A conciliação/build protegidos da R66 permanecem pendentes.

## Continuidade e escopo

Prioridades: retomada coordenada; auditoria do conteúdo ativo de Neo Geo; conciliação da segurança com R66; conferência CD e atualização compatível do segundo telefone. Os recibos distinguem relato humano, leitura Linux, preparação de código, instalação e gameplay.

Esta revisão altera documentação/evidência. API da07355, catálogo, chaves, licenças, importação, mídias e demais produtos continuam com sua publicação anterior. Não houve implantação, reinício, alteração de ROM/firmware ou instalação de APK durante esta análise.
