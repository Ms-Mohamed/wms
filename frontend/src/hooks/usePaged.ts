import { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import type { AxiosResponse } from 'axios';

export interface Paged<T> { rows: T[]; total: number; }

/**
 * Server-side pagination: the API takes ?page&pageSize and returns the
 * full count in the X-Total-Count header, so the browser never holds more
 * than one page of rows.
 */
export function usePaged<T>(
  key: string,
  fetcher: (p: { page: number; pageSize: number }) => Promise<AxiosResponse<T[]>>,
  initialPageSize = 25,
) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(initialPageSize);

  const query = useQuery<Paged<T>>({
    queryKey: [key, 'paged', page, pageSize],
    queryFn: async () => {
      const res = await fetcher({ page, pageSize });
      const header = Number(res.headers['x-total-count']);
      return { rows: res.data, total: Number.isFinite(header) && header > 0 ? header : res.data.length };
    },
    placeholderData: keepPreviousData,
  });

  const total = query.data?.total ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / pageSize));
  const safePage = Math.min(page, pageCount);

  return {
    ...query,
    rows: query.data?.rows ?? [],
    total,
    page: safePage,
    pageSize,
    pageCount,
    setPage,
    setPageSize: (n: number) => { setPageSize(n); setPage(1); },
  };
}
