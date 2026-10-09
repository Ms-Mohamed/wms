import type { ReactNode } from 'react';
import { Flex, Heading, Text } from '@chakra-ui/react';

export default function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: string; actions?: ReactNode }) {
  return (
    <Flex mb={6} align="flex-end" justify="space-between" wrap="wrap" gap={3}>
      <div>
        <Heading as="h1" size="lg" letterSpacing="-0.02em">{title}</Heading>
        {subtitle && <Text mt={1} color="ink.500" fontSize="sm">{subtitle}</Text>}
      </div>
      {actions}
    </Flex>
  );
}
