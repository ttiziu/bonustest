import json, os, socket, subprocess, time, urllib.request, urllib.error, uuid
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor

root = Path(__file__).resolve().parents[1]
db = 'BonusTest_Verify_' + uuid.uuid4().hex[:12]
password = os.environ.get('SQLCMDPASSWORD')
if not password:
    raise SystemExit('Define SQLCMDPASSWORD con la contraseña del SQL Server local antes de ejecutar.')
container = os.environ.get('SQL_CONTAINER', 'bonustest-sql')
count = 0

def sql(statement):
    result = subprocess.run(['docker', 'exec', '-i', '-e', 'SQLCMDPASSWORD', container, '/opt/mssql-tools18/bin/sqlcmd', '-S', 'localhost', '-U', 'sa', '-C', '-b'], input=('SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; '
        'SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;\n' + statement), text=True, capture_output=True)
    if result.returncode: raise RuntimeError(result.stdout + result.stderr)
    return result.stdout

def check(condition, message):
    global count
    assert condition, message
    count += 1

proc = None
try:
    sql((root/'database/01_schema_seed.sql').read_text().replace('BonusTest', db))
    sql((root/'database/02_bonus.sql').read_text().replace('USE BonusTest;', 'USE ' + db + ';'))
    sql((root/'database/02_bonus.sql').read_text().replace('USE BonusTest;', 'USE ' + db + ';'))
    sql((root/'database/03_bonus_project_allocation.sql').read_text().replace('USE BonusTest;', 'USE ' + db + ';'))
    sql((root/'database/03_bonus_project_allocation.sql').read_text().replace('USE BonusTest;', 'USE ' + db + ';'))
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0)); port = sock.getsockname()[1]
    env = os.environ.copy()
    env['ConnectionStrings__BonusTest'] = f'Server=localhost,1433;Database={db};User Id=sa;Password={password};TrustServerCertificate=True;'
    log = open('/tmp/bonustest_verify.log', 'w')
    proc = subprocess.Popen(['dotnet', str(root/'backend/src/BonusTest.Api/bin/Debug/net10.0/BonusTest.Api.dll'), '--urls', f'http://127.0.0.1:{port}'], env=env, cwd=root/'backend/src/BonusTest.Api', stdout=log, stderr=log)
    base = f'http://127.0.0.1:{port}/api/v1/Bonus'
    def request(method='GET', path='', data=None, user='verification'):
        headers = {'Content-Type':'application/json'}
        if user is not None: headers['UserCode'] = user
        req = urllib.request.Request(base+path, json.dumps(data).encode() if data is not None else None, headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=20) as r: return r.status, json.loads(r.read() or b'null')
        except urllib.error.HTTPError as e: return e.code, json.loads(e.read())
    for _ in range(100):
        try:
            status, body = request()
            if status == 200: break
        except OSError: time.sleep(.1)
    else: raise RuntimeError('API did not start')
    payload = dict(departmentCode='TECH1', employeeNumber=101, period='2020-01', bonusTypeId=2, value=10, reasonCode='000001')
    check(body['totalRecords'] == 0, 'empty initial list')
    check(request(path='/employee/TECH1/101')[1]['monthlySalary'] == 5000, 'employee lookup')
    check(request(path='/employee/VENTA/202')[1]['code'] == 'EMPLOYEE_INACTIVE', 'terminated employee')
    check(request(path='/employee/TECH1/999')[1]['code'] == 'EMPLOYEE_NOT_FOUND', 'missing employee')
    invalid = [('period','2999-01','FUTURE_PERIOD'),('period','2020-13','BONUS_PERIOD'),('reasonCode','000004','BONUS_REASON'),('value',0,'BONUS_VALUE'),('value',101,'BONUS_VALUE'),('bonusTypeId',3,'BONUS_TYPE')]
    for key, value, code in invalid:
        status, body = request('POST', data={**payload,key:value})
        check(status == 409 and body['code'] == code, code)
    check(request('POST', data=payload, user=None)[1]['code'] == 'USER_CODE', 'required operator')
    status, body = request('POST', data=payload); id1 = body.get('bonusId')
    check(status == 201 and id1, 'register')
    row = request()[1]['data'][0]
    check((row['grossAmount'],row['taxAmount'],row['netAmount']) == (500,40,460), 'document calculation')
    check(row['stateCode']=='0201' and row['fullName']=='Andrea Torres Vega' and row['reasonDescription']=='Cumplimiento de metas', 'descriptive registered row')
    check(request('POST',data=payload)[1]['code']=='DUPLICATE_BONUS', 'duplicate registration')
    check(request('PUT',f'/{id1}/cancel')[0]==204, 'cancel')
    check(request('DELETE',f'/{id1}')[1]['code']=='BONUS_STATE', 'cannot delete cancelled')
    check(request('PUT',f'/{id1}/cancel')[1]['code']=='BONUS_STATE', 'cannot cancel twice')
    status, body = request('POST',data=payload); id2=body['bonusId']
    check(status==201, 'reuse cancelled period')
    check(request('DELETE',f'/{id2}')[0]==204, 'logical delete')
    check(request()[1]['totalRecords']==1, 'deleted excluded')
    check(request('DELETE',f'/{id2}')[1]['code']=='BONUS_NOT_FOUND', 'cannot delete twice')
    check(request('POST',data=payload)[0]==201, 'reuse deleted period')
    race = {**payload,'period':'2020-02'}
    with ThreadPoolExecutor(max_workers=2) as pool:
        statuses = sorted(pool.map(lambda _: request('POST',data=race)[0],range(2)))
    check(statuses==[201,409], f'concurrent uniqueness {statuses}')
    fixed = {**payload,'period':'2020-03','bonusTypeId':1,'value':1.25,'employeeNumber':103}
    check(request('POST',data=fixed)[0]==201, 'fixed registration')
    fixedrow=request(path='?employeeNumber=103')[1]['data'][0]
    check((fixedrow['grossAmount'],fixedrow['taxAmount'],fixedrow['netAmount'])==(1.25,.13,1.12), 'half-up rounding')
    for month in range(4,12):
        check(request('POST',data={**payload,'period':f'2020-{month:02d}'})[0]==201, 'pagination fixture')
    first=request()[1]; second=request(path='?pageIndex=2')[1]
    check(first['totalRecords']==12 and first['totalPages']==2 and len(first['data'])==10 and len(second['data'])==2, 'server paging')
    check(not ({x['bonusId'] for x in first['data']} & {x['bonusId'] for x in second['data']}), 'stable separate pages')
    check(request(path='?stateCode=0203')[1]['totalRecords']==1, 'state filter')
    check(request(path='?departmentCode=VENTA')[1]['totalRecords']==0, 'department filter')
    check(request(path='?period=2020-01&employeeNumber=101')[1]['totalRecords']==2, 'combined period and employee filters')
    check(request(path='?period=invalid')[1]['code']=='BONUS_PERIOD', 'invalid list period')
    check(request(path='?pageIndex=2147483647')[1]['code']=='PAGE', 'pagination overflow validation')
    audit=sql(f"USE {db}; SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Bonus WHERE IsDeleted=1 AND CreatedBy='verification' AND ModifiedBy='verification' AND ModifiedAt>=CreatedAt;")
    check('1' in audit, 'soft-deleted audit persists')
    # Pago proporcional y estado final, auditados en SQL además de la respuesta HTTP.
    paidid = request('POST', data={**payload, 'period': '2019-01'})[1]['bonusId']
    status, payment = request('POST', f'/{paidid}/pay', user='payer')
    check(status == 200 and payment['stateCode'] == '0202', 'payment status')
    check([p['grossAmount'] for p in payment['projects']] == [375, 125], 'proportional project payment')
    sql(f"USE {db}; IF NOT EXISTS (SELECT 1 FROM dbo.Bonus WHERE BonusId={paidid} AND StateCode='0202' AND ModifiedBy='payer') THROW 51000, 'Payment audit missing', 1; IF (SELECT SUM(GrossAmount) FROM dbo.BonusProjectAllocation WHERE BonusId={paidid}) <> 500 THROW 51000, 'Wrong distribution sum', 1;")
    check(True, 'payment and audit persisted')
    check(request('POST', f'/{paidid}/pay')[1]['code'] == 'BONUS_STATE', 'cannot pay twice')
    check(request('PUT', f'/{paidid}/cancel')[1]['code'] == 'BONUS_STATE', 'cannot cancel paid')
    check(request('DELETE', f'/{paidid}')[1]['code'] == 'BONUS_STATE', 'cannot delete paid')
    check(request('POST', f'/{id1}/pay')[1]['code'] == 'BONUS_STATE', 'cannot pay cancelled')
    check(request('POST', f'/{id2}/pay')[1]['code'] == 'BONUS_NOT_FOUND', 'cannot pay deleted')
    check(request('POST', f'/{paidid}/pay', user=None)[1]['code'] == 'USER_CODE', 'payment requires operator')
    check(request('POST', '/2147483647/pay')[1]['code'] == 'BONUS_NOT_FOUND', 'payment requires existing bonus')
    check(request(path='?stateCode=0202')[1]['totalRecords'] == 1, 'paid filter')
    single = {**payload, 'departmentCode': 'VENTA', 'employeeNumber': 201, 'period': '2019-02', 'bonusTypeId': 1, 'value': 100}
    singleid = request('POST', data=single)[1]['bonusId']
    payment = request('POST', f'/{singleid}/pay')[1]
    check(len(payment['projects']) == 1 and payment['projects'][0]['grossAmount'] == 100, 'single-project payment')
    # Tres proyectos iguales: el último recibe el centavo pendiente.
    sql(f"USE {db}; UPDATE dbo.EmployeeProjectHours SET Hours=1 WHERE DepartmentCode='TECH1' AND EmployeeNumber=102;")
    roundedid = request('POST', data={**payload, 'employeeNumber': 102, 'period': '2019-03', 'bonusTypeId': 1, 'value': 1})[1]['bonusId']
    payment = request('POST', f'/{roundedid}/pay')[1]
    check([p['grossAmount'] for p in payment['projects']] == [.33, .33, .34], 'last-project rounding')
    # Dos pagos simultáneos: un único ganador y ninguna línea duplicada.
    concurrentid = request('POST', data={**payload, 'period': '2019-04'})[1]['bonusId']
    with ThreadPoolExecutor(max_workers=2) as pool:
        responses = list(pool.map(lambda _: request('POST', f'/{concurrentid}/pay'), range(2)))
    check(sorted(r[0] for r in responses) == [200, 409], 'concurrent payment protected')
    check(next(r[1]['code'] for r in responses if r[0] == 409) in ('BONUS_STATE', 'BONUS_CHANGED'), 'payment concurrency error is specific')
    sql(f"USE {db}; IF (SELECT COUNT(*) FROM dbo.BonusProjectAllocation WHERE BonusId={concurrentid}) <> 2 THROW 51000, 'Duplicated payment lines', 1;")
    check(True, 'concurrent payment saved once')
    # Un fallo real de SQL durante las inserciones debe revertir también el cambio de estado.
    rollbackid = request('POST', data={**payload, 'period': '2019-05'})[1]['bonusId']
    sql(f"USE {db};\nGO\nCREATE TRIGGER dbo.VerifyPaymentFailure ON dbo.BonusProjectAllocation AFTER INSERT AS BEGIN THROW 51000, 'Forced verification failure', 1; END;\nGO")
    check(request('POST', f'/{rollbackid}/pay')[0] == 500, 'forced persistence failure')
    sql(f"USE {db}; IF NOT EXISTS (SELECT 1 FROM dbo.Bonus WHERE BonusId={rollbackid} AND StateCode='0201') THROW 51000, 'State was not rolled back', 1; IF EXISTS (SELECT 1 FROM dbo.BonusProjectAllocation WHERE BonusId={rollbackid}) THROW 51000, 'Details were not rolled back', 1; DROP TRIGGER dbo.VerifyPaymentFailure;")
    check(True, 'payment transaction rolled back completely')
    check(request('POST', f'/{rollbackid}/pay')[0] == 200, 'payment succeeds after removing forced failure')
    # Sin horas válidas: no cambia el estado ni se persiste detalle.
    nohoursid = request('POST', data={**payload, 'employeeNumber': 103, 'period': '2019-06'})[1]['bonusId']
    sql(f"USE {db}; UPDATE dbo.EmployeeProjectHours SET Hours=0 WHERE DepartmentCode='TECH1' AND EmployeeNumber=103;")
    check(request('POST', f'/{nohoursid}/pay')[1]['code'] == 'PROJECT_HOURS', 'zero total hours rejected')
    sql(f"USE {db}; IF NOT EXISTS (SELECT 1 FROM dbo.Bonus WHERE BonusId={nohoursid} AND StateCode='0201') THROW 51000, 'Invalid hours changed state', 1; IF EXISTS (SELECT 1 FROM dbo.BonusProjectAllocation WHERE BonusId={nohoursid}) THROW 51000, 'Invalid hours inserted details', 1;")
    check(True, 'invalid payment preserved original state')
    print(f'{count} API/SQL checks passed, including concurrent registration/payment and rollback.')
finally:
    if proc:
        proc.terminate()
        try: proc.wait(timeout=10)
        except subprocess.TimeoutExpired: proc.kill(); proc.wait()
    sql(f"IF DB_ID('{db}') IS NOT NULL BEGIN ALTER DATABASE [{db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{db}]; END;")
    print('Temporary verification database removed. Original BonusTest database unchanged.')
