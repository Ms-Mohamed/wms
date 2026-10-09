import { Flex, IconButton, Select, Text } from '@chakra-ui/react';
import { ChevronLeftIcon, ChevronRightIcon } from '@heroicons/react/20/solid';

interface Props {
  page: number;
  pageCount: number;
  pageSize: number;
  total: number;
  onPage: (p: number) => void;
  onPageSize: (n: number) => void;
}

export default function Pagination({ page, pageCount, pageSize, total, onPage, onPageSize }: Props) {
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(total, page * pageSize);
  return (
    <Flex
      px={4} py={3} align="center" justify="space-between" wrap="wrap" gap={3}
      borderTop="1px solid" borderColor="ink.100" fontSize="sm" color="ink.500"
    >
      <Text>
        <Text as="span" color="ink.800" fontWeight={600}>{from}–{to}</Text> / {total}
      </Text>
      <Flex align="center" gap={2}>
        <Select size="sm" w="auto" value={pageSize} onChange={(e) => onPageSize(Number(e.target.value))} aria-label="Rows per page">
          {[10, 25, 50, 100].map((n) => <option key={n} value={n}>{n} / page</option>)}
        </Select>
        <IconButton aria-label="Previous page" size="sm" variant="subtle" icon={<ChevronLeftIcon width={18} />} isDisabled={page <= 1} onClick={() => onPage(page - 1)} />
        <Text minW="64px" textAlign="center">{page} / {pageCount}</Text>
        <IconButton aria-label="Next page" size="sm" variant="subtle" icon={<ChevronRightIcon width={18} />} isDisabled={page >= pageCount} onClick={() => onPage(page + 1)} />
      </Flex>
    </Flex>
  );
}
