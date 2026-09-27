import { useEffect, useState } from 'react';
import { errorMessage } from '../../services/api/errors.js';
import { getEmailStatus } from '../../services/admin/adminService.js';

export default function AdminEmailStatus({ session }) {
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let active = true;
    setError('');
    getEmailStatus(session)
      .then((r) => {
        if (active) setData(r.data);
      })
      .catch((e) => {
        if (active) setError(errorMessage(e));
      });
    return () => {
      active = false;
    };
  }, [session, revision]);
  return (
    <details className="mb-5 rounded-xl border p-4">
      <summary className="cursor-pointer font-semibold">Estado del correo</summary>
      <div className="mt-3 text-sm">
        {data && (
          <>
            <p>{data.enabled ? 'Envío habilitado: Postmark' : 'Envío deshabilitado o pendiente de configuración'}</p>
            <ul className="my-3">
              {data.counts.map((c) => (
                <li key={c.status}>
                  {{
                    Pending: 'Pendientes',
                    Sent: 'Aceptados por Postmark',
                    Failed: 'Fallidos',
                    Skipped: 'Omitidos',
                    Expired: 'Vencidos',
                  }[c.status] || c.status}
                  : {c.count}
                </li>
              ))}
            </ul>
          </>
        )}
        {error && (
          <p role="alert" className="text-red-700">
            {error}
          </p>
        )}
        <button onClick={() => setRevision((n) => n + 1)} className="underline">
          Actualizar estado
        </button>
      </div>
    </details>
  );
}
