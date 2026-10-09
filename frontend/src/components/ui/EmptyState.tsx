import { Box, Text } from '@chakra-ui/react';

export default function EmptyState({ children }: { children: string }) {
  return <Box py={14} textAlign="center"><Text color="ink.400" fontSize="sm">{children}</Text></Box>;
}
