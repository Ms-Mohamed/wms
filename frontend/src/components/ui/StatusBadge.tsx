import { Badge } from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';

// The API serialises the enum by name ("PartiallyShipped").
const SCHEME: Record<string, string> = {
  pending: 'orange',
  confirmed: 'blue',
  processing: 'purple',
  partiallyshipped: 'yellow',
  shipped: 'cyan',
  delivered: 'green',
  cancelled: 'red',
};

export default function StatusBadge({ status }: { status: string }) {
  const { t } = useTranslation();
  const key = status.toLowerCase();
  return (
    <Badge colorScheme={SCHEME[key] ?? 'gray'} variant="subtle" borderRadius="full" px={2.5} py={0.5} textTransform="none" fontWeight={600}>
      {t(`orders.statuses.${key}`, { defaultValue: status })}
    </Badge>
  );
}
