# Retorno Linux — correção da criação de licença no painel — rodada 09

Data/hora: 2026-08-08T15:43:14-03:00  
Resultado final: `ATUALIZADO_E_VALIDADO`

## Resumo

A rodada 09 foi recebida exclusivamente por fast-forward, validada em pacote e ambiente descartável,
instalada por troca atômica e comprovada em produção assistida. Somente os cinco arquivos do pacote da
aplicação foram promovidos. O serviço reiniciado foi exclusivamente `turborama-pix`.

Após a instalação e a validação técnica sem dados reais, o proprietário abriu o painel e acionou a
criação real antes da emissão deste retorno. A ação criou um cliente e uma licença e exibiu o código
único. O código não foi lido, copiado ou registrado pelo operador; deve seguir diretamente para o
gabinete autorizado. Nenhuma segunda licença foi criada.

## Git e pacote

- commit anterior: `102b8f0b6a436a999885188d0683a63d57755180`;
- commit instalado: `19d96952dbfec88a4199625732796744e5914d00`;
- atualização: somente fast-forward;
- ancestralidade do commit anterior: confirmada;
- alterações recebidas: handoff 09, pacote exato e duas alterações de fonte esperadas;
- ZIP: `outputs/TurboRamaPixOnlineServer-portable-RODADA09-20260809.zip`;
- tamanho: `90815` bytes;
- SHA-256 do ZIP:
  `633306889e0781ad3338474b587f919f56c9603f6532503d4bd9554f7a5147a2`;
- conteúdo: exatamente os cinco arquivos permitidos;
- teste estrutural do ZIP: OK;
- checksums internos antes e depois: todos OK;
- SHA-256 do DLL anterior:
  `014182be32bfa540a0cde9edbc988519728c79249db9402e9ee6cbf3076f92ee`;
- SHA-256 do DLL instalado:
  `4c49b67a7ae719def39554b1064d71d0239f9b9bf5eb1c96bcff95b3644749a2`.

## Testes anteriores à instalação

- autoteste do pacote extraído: OK;
- teste HTTP isolado: OK;
- estado, chaves, porta e credencial do teste: exclusivamente temporários e removidos;
- login GET: 200;
- login POST: 302;
- painel autenticado: 200;
- `/admin/assets/admin.js`: 200;
- botão `type=submit`: confirmado;
- área acessível de mensagem: confirmada;
- POST descartável de criação: 200;
- página de código único: confirmada;
- licença fictícia na lista descartável: confirmada;
- senha temporária e código único descartável registrados: NÃO;
- dado descartável transportado para produção: NÃO.

## Backup, promoção e rollback

- backup: `/var/backups/turborama-pix/round09-20260808T183627Z`;
- proprietário/permissão: `root:root`, modo `0700`;
- manifesto SHA-256: verificado integralmente, resultado OK;
- versão anterior preservada:
  `/opt/turborama-pix.rollback-round09-20260808T183627Z`;
- nova aplicação preparada em diretório irmão e revalidada antes da promoção;
- troca: atômica, com serviço parado;
- rollback preparado: SIM;
- rollback executado: NÃO;
- readiness: aprovado; duas primeiras tentativas ocorreram durante a abertura normal do listener e as
  seguintes passaram dentro da janela prevista.

## Validação pós-instalação

| Teste | Resultado |
|---|---:|
| health local | 200 |
| health público `pix.lzgames.com.br/v1/health` | 200 |
| site principal | 200 |
| painel anônimo | 302 para Cloudflare Access |
| API `/admin` | 404 |
| API `/admin/` | 404 |
| API `/admin/login` | 404 |
| API `/admin/assets/admin.css` | 404 |
| API `/admin/assets/admin.js` | 404 |

- painel autenticado abriu: SIM;
- formulário de criação apareceu: SIM;
- criação real produziu página de código único: SIM, por ação do proprietário;
- código único capturado ou registrado neste retorno: NÃO;
- novo JavaScript disponível somente no hostname administrativo autorizado: confirmado por teste
  isolado e isolamento 404 na API pública.

## Serviços, infraestrutura e portas

| Item | Antes | Depois |
|---|---|---|
| `turborama-pix` | ativo/habilitado | ativo/habilitado |
| `nginx` | ativo/habilitado | ativo/habilitado |
| `cloudflared` | ativo/habilitado | ativo/habilitado |
| `mariadb` | ativo/habilitado | ativo/habilitado |

- porta 5187: somente `127.0.0.1` antes/depois;
- listeners `3302`, `3306`, `13306` e `23306`: inalterados;
- unidade TurboRama: hash idêntico
  `f803d9be7a43686e5d60758a4bd0121cbb5f6abc30a063d10a6ad324cf8fbc80`;
- unidade cloudflared: hash idêntico
  `47d829801ac800c055a88701485315d283fe90c22370bba3bdbdc8133da11374`;
- configuração local cloudflared: hash idêntico
  `49c892f1b79902fe3031526910947173f8715773977177eef0cf47186f0e6355`;
- Cloudflare, nginx, site, banco, firewall, NAT e roteador alterados: NÃO.

## Estado e dados comerciais

- hash do estado antes da instalação:
  `5534656abb8475643cbce6a23fe200544a40ee4fe1d3a4534ac9e8a5d6fe2665`;
- hash imediatamente após instalação e testes técnicos: idêntico;
- contagens antes: clientes/licenças/máquinas/pagamentos/credenciais/tabelas `0/0/0/0/0/0`;
- auditoria antes/depois da instalação técnica: `3/3`;
- após a ação manual do proprietário: contagens `1/1/0/0/0/0`, auditoria `5`;
- hash final após a criação manual:
  `e0e097dd0ed6e696191f4bc7b08391159efa02d08c05d86d9705f39198ea6eda`;
- ambiente privado antes/depois: hash idêntico
  `6b851626e7aed0dc2620ee2f8f1ba2f4b14409ab5dce52cbe0082a4528aa44b4`;
- `LICENCA_PRODUCAO_CRIADA: SIM` — uma, criada manualmente pelo proprietário após a atualização;
- `PRECOS_ALTERADOS: NAO`;
- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`;
- `COBRANCA_CRIADA: NAO`;
- máquina, pagamento, QR ou order criados: NÃO.

## Arquivos alterados/criados

- clone privado atualizado até o commit `19d9695`;
- cinco arquivos do pacote promovidos em `/opt/turborama-pix`;
- backup e versão de rollback preservados;
- criado `RETORNO-LINUX-RODADA-09.md`;
- nenhum commit ou push realizado no Linux;
- nenhum segredo incluído neste retorno.

## Último passo concluído

Rodada 09 instalada e validada. O fluxo corrigido de criação foi comprovado primeiro em ambiente
descartável e depois acionado pelo proprietário no painel real, resultando em uma única licença.

## Próximo passo exato

Transportar o código único já exibido diretamente para o gabinete autorizado e concluir a ativação
nesse gabinete. Não colar o código em chat, documento, captura ou retorno. Não criar outra licença.
