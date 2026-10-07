# Ativação do registro Station para R73

Pedido APP → SERVIDOR recebido em `1b26b34cd0f205afaf70b4f9ebf2391da7853537`.
Fontes do app: `5657dce678609f25501321e307839a6e0c018d4e`.

## Alteração delimitada

Adicionar exatamente os dois objetos recebidos em
`entrega-app-r73-20261007/evidence/server-engine-registry-additions.json`, SHA256
`a3066af1ac72e1c93e919aecef61c3a954fe1a0d29dd5a77fa6073ffb9753e0b`,
preservando os seis objetos anteriores.

- SNES: `bsnes-mercury-performance-79d7f9de-rs3-9af2778898e4`.
- Mega Drive: `clownmdemu-d43c2708-rs3-9af2778898e4`.
- Runtime comum: `9af2778898e4ba026d65d9b5c74ef3d8089e58bdbdf9be40f8e28f0eedcb14c2`.
- Protocolo: `station-stream.v2`; hashes dos núcleos permanecem os recebidos.

Registro efetivo:
`/opt/turborama-station-recovery-r71-20261007-ab192bf/online-engine-registry.json`.
SHA anterior: `310fefece80c336840882d0c91235b765d59f160246df947393802ea0211d289`.
O código em `StationOnlineRegistration.AddStationOnline` lê o arquivo na inicialização
e conserva o singleton. A ativação requer reiniciar somente
`turborama-station-api.service`; a DLL permanece em `ab192bf`, SHA
`815fc8bc99a9d16247488a1797d2726b928eb3e592fd8a19700371e8d57c3243`.

## Guardas e provas

`scripts/ativar-registro-station-r73-20261007.py` exige autenticação nativa polkit,
commit exato e íntegro do operador, DLL/identidade/registro/catálogo conhecidos,
nenhuma credencial de outro produto, e políticas de recuperação inalteradas.
Qualifica o mesmo binário com o registro de oito engines em unidade temporária
com sandbox equivalente. Depois cria cópia privada exata do registro anterior,
substitui atomicamente o JSON e verifica o serviço real por HTTPS/WSS assinado.

O verificador usa duas licenças sintéticas por ensaio e exige limpeza. Confere todos
os oito objetos do snapshot assinado, campos exatos, nomes próprios fora da página
social, protocolo v2, autenticação/replay/ticket, barreira/retomada simulada, v1,
catálogo completo, capa e download real. Não representa uma partida Android.

Reinício recusado se existir conexão, bytes pendentes ou sessão recuperável.
O mantenedor confirmou a saída da tentativa R72 nos dois celulares. O serviço
anterior conserva até uma sala terminal sem anexos: `RecoveryRoomCurrent` também
retorna verdadeiro para `unrecoverable`. O argumento opcional
`--closed-trial-start-utc 2026-10-07T22:48:19.238974+00:00` autoriza descartar somente
essa tentativa explicitamente encerrada, se for a última iniciada, tiver evento
`recovery-failed` do mesmo correlation, zero conexões/bytes e nenhuma nova criação,
retomada ou emissão de ticket posterior ao evento terminal. O registro privado
contém apenas o SHA do correlation. Não é expiração de sessões por tempo.

Ambientes, drop-ins, chaves, licenças reais, DLL e demais arquivos do release,
schema, catálogo e PIDs de outros serviços são comparados antes/depois. Este
procedimento não altera a lógica de retenção, contrato TSR2, segurança, proxy,
Cloudflare ou outros produtos. Geolith não é habilitado.

## Execução e retorno

Python do operador:
`/mnt/DADOS/station-auto-room-access-check-20261006/operator-venv/bin/python`.
Executar `pkexec <python> -B <script-absoluto> --apply <commit-exato>`;
usar o argumento da tentativa encerrada apenas quando as condições acima forem
comprovadas. A atividade efetiva consta de `PRODUCAO-EFETIVA.json` após o sucesso.

Backup privado em `/mnt/DADOS/station-r73-registry-backup-20261007-<instante>`.
Retorno guardado: `pkexec <python> -B <script-absoluto> --rollback <backup>`.
Recusa DLL/configuração sucessora ou sessão ativa; restaura o registro original
com seis engines e recarrega somente Station. Nunca restaura banco de produção.

Depois do cadastro comprovado, o PC do APK deve instalar R73 nos dois aparelhos,
mesma assinatura e dados preservados, e testar sala nova em ambos os papéis.
Registrar primeira imagem/áudio, controles, nomes, saída e recuperação de rede
separadamente. `event=wait-diagnostic` do app informa epoch/state/nativeStatus e
offsets; o backend atual não registra cada quadro PAUSED/READY/STATE. Nenhum PONG,
HTTP200 ou ensaio sintético comprova a liberação nativa da primeira imagem.
